using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;
using WebApolice.BitrixIntegration.Modules.Integracao;
using WebApolice.BitrixIntegration.Modules.Integracao.Repositories;
using WebApolice.BitrixIntegration.Modules.Integracao.Services;
using WebApolice.BitrixIntegration.Modules.Integracao.Workers;

namespace WebApolice.BitrixIntegration.Workers;

public class CustomerSynchronizationWorker : BackgroundService
{
    private readonly ILogger<CustomerSynchronizationWorker> _logger;
    private readonly CustomerSynchronizationSettings _settings;
    private readonly CustomerSyncRepository _syncRepository;
    private readonly WebApoliceCustomerRepository _customerRepository;
    private readonly CustomerSynchronizationService _syncService;
    private readonly CustomerSynchronizationWorkerState _workerState;

    public CustomerSynchronizationWorker(
        ILogger<CustomerSynchronizationWorker> logger,
        IOptions<CustomerSynchronizationSettings> settings,
        CustomerSyncRepository syncRepository,
        WebApoliceCustomerRepository customerRepository,
        CustomerSynchronizationService syncService,
        CustomerSynchronizationWorkerState workerState)
    {
        _logger = logger;
        _settings = settings.Value;
        _syncRepository = syncRepository;
        _customerRepository = customerRepository;
        _syncService = syncService;
        _workerState = workerState;

        ValidateSettings(_settings);
    }

    private void ValidateSettings(CustomerSynchronizationSettings settings)
    {
        if (settings.PollingIntervalSeconds < 5)
            throw new ArgumentException("PollingIntervalSeconds deve ser pelo menos 5 segundos.");
        if (settings.BatchSize < 1 || settings.BatchSize > 500)
            throw new ArgumentException("BatchSize deve estar entre 1 e 500.");
        if (settings.MaxRetryAttempts < 1)
            throw new ArgumentException("MaxRetryAttempts deve ser pelo menos 1.");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.Enabled)
        {
            _workerState.IsRunning = false;
            _logger.LogInformation("CustomerSynchronizationWorker est desativado via config.");
            return;
        }

        _workerState.IsRunning = true;
        _logger.LogInformation("CustomerSynchronizationWorker iniciado. PollingInterval={PollingInterval}s, BatchSize={BatchSize}", _settings.PollingIntervalSeconds, _settings.BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            var globalErrorOccurred = false;
            var processingToken = Guid.NewGuid();

            try
            {
                _workerState.LastCycleStartedAt = DateTime.UtcNow;

                // 1. Recupera travados
                await _syncRepository.RecoverStuckRecordsAsync(_settings.ProcessingTimeoutMinutes, stoppingToken);

                // 2. Reserva lote
                var batch = await _syncRepository.ReserveBatchAsync(_settings.BatchSize, processingToken, stoppingToken);

                if (batch.Count > 0)
                {
                    _logger.LogInformation("Processando lote de {Count} clientes. Token: {Token}", batch.Count, processingToken);

                    foreach (var record in batch)
                    {
                        if (stoppingToken.IsCancellationRequested)
                        {
                            // Trata Cancelamento no meio do lote
                            await _syncRepository.ClearProcessingTokenAsync(record.Id, processingToken, CancellationToken.None);
                            continue;
                        }

                        try
                        {
                            var request = await _customerRepository.GetCustomerUpsertRequestAsync(record.ClienteId, record.PessoaId, stoppingToken);
                            if (request == null)
                            {
                                throw new InvalidOperationException($"Cliente {record.ClienteId} no encontrado na base WebApolice.");
                            }

                            // Verifica Hash
                            var currentHash = CustomerPayloadHasher.ComputeHash(request);
                            if (currentHash == record.PayloadHash && !string.IsNullOrWhiteSpace(record.BitrixId) && record.Status == "SINCRONIZADO")
                            {
                                await _syncRepository.UpdateSkippedAsync(record.Id, processingToken, stoppingToken);
                                continue;
                            }

                            var result = await _syncService.SynchronizeCustomerAsync(request, stoppingToken);
                            await _syncRepository.UpdateSuccessAsync(record.Id, processingToken, request.CustomerType.ToString(), result.CrmId, currentHash, stoppingToken);
                        }
                        catch (Exception ex)
                        {
                            var errorType = SynchronizationErrorClassifier.Classify(ex);
                            var isPermanent = errorType == SynchronizationErrorType.Permanent;
                            var nextAttempt = DateTime.UtcNow.AddSeconds(_settings.RetryDelaySeconds);

                            if (errorType == SynchronizationErrorType.Cancelled)
                            {
                                _logger.LogWarning("Cancelamento solicitado durante processamento do cliente {ClienteId}. Abortando e limpando token.", record.ClienteId);
                                await _syncRepository.ClearProcessingTokenAsync(record.Id, processingToken, CancellationToken.None);
                                globalErrorOccurred = true;
                                break;
                            }

                            if (errorType == SynchronizationErrorType.Configuration)
                            {
                                _logger.LogError(ex, "Erro de Configurao Global detectado no cliente {ClienteId}. Interrompendo todo o lote atual.", record.ClienteId);
                                await _syncRepository.UpdateFailedAsync(record.Id, processingToken, ex.Message, isPermanentError: false, _settings.MaxRetryAttempts, nextAttempt, stoppingToken);
                                
                                // Libera o restante do lote
                                await _syncRepository.ReleaseBatchAsync(processingToken, record.Id, nextAttempt, "Lote interrompido devido a erro global de configurao (ex: Token invlido).", stoppingToken);
                                
                                globalErrorOccurred = true;
                                break; // Interrompe o batch
                            }

                            _logger.LogError(ex, "Erro individual no processamento do cliente {ClienteId} (Transient/Permanent).", record.ClienteId);
                            await _syncRepository.UpdateFailedAsync(record.Id, processingToken, ex.Message, isPermanent, _settings.MaxRetryAttempts, nextAttempt, stoppingToken);
                        }
                    }
                }

                _workerState.LastSuccessfulCycleAt = DateTime.UtcNow;
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("CustomerSynchronizationWorker interrompido via CancellationToken.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro nvel crtico no CustomerSynchronizationWorker.");
                _workerState.LastError = ex.Message;
                globalErrorOccurred = true;
            }
            finally
            {
                _workerState.LastCycleFinishedAt = DateTime.UtcNow;
            }

            if (stoppingToken.IsCancellationRequested) break;

            var delaySeconds = globalErrorOccurred ? _settings.RetryDelaySeconds : _settings.PollingIntervalSeconds;
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);
        }

        _workerState.IsRunning = false;
        _logger.LogInformation("CustomerSynchronizationWorker finalizado com segurana.");
    }
}
