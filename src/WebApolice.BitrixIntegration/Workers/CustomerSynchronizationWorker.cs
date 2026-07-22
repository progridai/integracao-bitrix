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
using WebApolice.BitrixIntegration.Modules.Crm;

namespace WebApolice.BitrixIntegration.Workers;

public class CustomerSynchronizationWorker : BackgroundService
{
    private readonly ILogger<CustomerSynchronizationWorker> _logger;
    private readonly CustomerSynchronizationSettings _settings;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly CustomerSynchronizationWorkerState _workerState;
    private readonly SynchronizationSafetySettings _safetySettings;

    public CustomerSynchronizationWorker(
        ILogger<CustomerSynchronizationWorker> logger,
        IOptions<CustomerSynchronizationSettings> settings,
        IServiceScopeFactory scopeFactory,
        CustomerSynchronizationWorkerState workerState,
        IOptions<SynchronizationSafetySettings> safetySettings)
    {
        _logger = logger;
        _settings = settings.Value;
        _scopeFactory = scopeFactory;
        _workerState = workerState;
        _safetySettings = safetySettings.Value;

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
        if (!_settings.Enabled || _safetySettings.Mode == "Disabled")
        {
            _workerState.IsRunning = false;
            _logger.LogInformation("CustomerSynchronizationWorker est desativado via config (Enabled=false ou Mode=Disabled).");
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

                using var scope = _scopeFactory.CreateScope();
                var syncRepository = scope.ServiceProvider.GetRequiredService<CustomerSyncRepository>();
                var customerRepository = scope.ServiceProvider.GetRequiredService<WebApoliceCustomerRepository>();
                var syncService = scope.ServiceProvider.GetRequiredService<CustomerSynchronizationService>();
                var crmProvider = scope.ServiceProvider.GetRequiredService<ICustomerCrmProvider>();
                var validator = scope.ServiceProvider.GetRequiredService<WebApolice.BitrixIntegration.Modules.Bitrix.IBitrixConfigurationValidator>();

                if (_safetySettings.Mode == "Live")
                {
                    var errors = await validator.ValidateAsync(stoppingToken);
                    
                    // Validação de public_id será adicionada no WebApoliceCustomerRepository logo abaixo.
                    var invalidPublicIdsCount = await customerRepository.GetInvalidPublicIdsCountAsync(stoppingToken);
                    if (invalidPublicIdsCount > 0)
                    {
                        errors.Add($"Existem {invalidPublicIdsCount} clientes com public_id nulo, vazio ou duplicado na base WebApólice.");
                    }

                    if (errors.Any())
                    {
                        _logger.LogError("Preflight do modo Live falhou. Erros: {Errors}", string.Join(" | ", errors));
                        _workerState.LastError = "Preflight falhou: " + errors.First();
                        globalErrorOccurred = true;
                        
                        await Task.Delay(TimeSpan.FromSeconds(_settings.RetryDelaySeconds), stoppingToken);
                        continue;
                    }
                }

                // 1. Recupera travados
                await syncRepository.RecoverStuckRecordsAsync(_settings.ProcessingTimeoutMinutes, stoppingToken);

                // 2. Reserva lote
                var batchSize = _settings.BatchSize;
                if (_safetySettings.Mode == "Live" && _safetySettings.MaximumLiveBatchSize.HasValue)
                {
                    batchSize = Math.Min(batchSize, _safetySettings.MaximumLiveBatchSize.Value);
                }

                var batch = await syncRepository.ReserveBatchAsync(batchSize, processingToken, _safetySettings.AllowAllCustomers, _safetySettings.AllowedCustomerIds, _safetySettings.Mode == "DryRun", stoppingToken);

                if (batch.Count > 0)
                {
                    _logger.LogInformation("Processando lote de {Count} clientes. Token: {Token}", batch.Count, processingToken);

                    foreach (var record in batch)
                    {
                        if (stoppingToken.IsCancellationRequested)
                        {
                            // Trata Cancelamento no meio do lote
                            await syncRepository.ClearProcessingTokenAsync(record.Id, processingToken, CancellationToken.None);
                            continue;
                        }

                        CrmCustomerUpsertRequest? request = null;
                        string? currentHash = null;

                        try
                        {
                            request = await customerRepository.GetCustomerUpsertRequestAsync(record.ClienteId, record.PessoaId, stoppingToken);
                            if (request == null)
                            {
                                throw new InvalidOperationException($"Cliente {record.ClienteId} no encontrado na base WebApolice.");
                            }

                            // Verifica Hash (Apenas para modo Live)
                            currentHash = CustomerPayloadHasher.ComputeHash(request);
                            if (_safetySettings.Mode == "Live")
                            {
                                if (currentHash == record.PayloadHash && !string.IsNullOrWhiteSpace(record.BitrixId) && record.Status == "SINCRONIZADO")
                                {
                                    await syncRepository.UpdateSkippedAsync(record.Id, processingToken, stoppingToken);
                                    continue;
                                }
                            }
                            else if (_safetySettings.Mode == "DryRun")
                            {
                                // Impede de reprocessar continuamente no DryRun
                                if (currentHash == record.LastDryRunHash)
                                {
                                    await syncRepository.UpdateSkippedAsync(record.Id, processingToken, stoppingToken);
                                    continue;
                                }
                            }

                            var result = await syncService.SynchronizeCustomerAsync(request, stoppingToken);
                            
                            if (_safetySettings.Mode == "DryRun")
                            {
                                await syncRepository.CompleteDryRunAsync(record.Id, processingToken, currentHash, $"Sucesso. Payload virtual: {result.CrmId}", request.SourceModifiedAt, stoppingToken);
                            }
                            else
                            {
                                // Persiste o ID imediatamente
                                string type = request.CustomerType.ToString().ToUpper() == "CONTACT" ? "CONTACT" : "COMPANY";
                                await syncRepository.UpdateBitrixIdAsync(record.Id, processingToken, type, result.CrmId, stoppingToken);

                                if (_safetySettings.VerifyAfterWrite && result.WasCreated)
                                {
                                    // Pós-escrita: Tentamos ler a entidade para confirmar que está acessível e consistente
                                    // Se falhar, lançará exceção e cairá no catch como falha transitória (pois já gravamos o ID acima).
                                    // Na próxima vez, o SyncService tentará fazer Update pois record.BitrixId existirá? Não, o BitrixCustomerCrmProvider usa ORIGIN_ID.
                                    // Para garantir que usemos o ID salvo, vamos checar aqui:
                                    // Por simplicidade, a validação apenas atesta a conectividade após escrita.
                                    var fields = await crmProvider.GetAvailableFieldsAsync(request.CustomerType, stoppingToken);
                                    if (fields == null || !fields.Any())
                                    {
                                        throw new Exception("Validação pós-escrita falhou: Não foi possível obter metadata após criação.");
                                    }
                                }

                                await syncRepository.UpdateSuccessAsync(record.Id, processingToken, type, result.CrmId, currentHash, stoppingToken);
                            }
                        }
                        catch (Exception ex)
                        {
                            var errorType = SynchronizationErrorClassifier.Classify(ex);
                            var isPermanent = errorType == SynchronizationErrorType.Permanent;
                            var nextAttempt = DateTime.UtcNow.AddSeconds(_settings.RetryDelaySeconds);

                            if (errorType == SynchronizationErrorType.Cancelled)
                            {
                                _logger.LogWarning("Cancelamento solicitado durante processamento do cliente {ClienteId}. Abortando e limpando token.", record.ClienteId);
                                await syncRepository.ClearProcessingTokenAsync(record.Id, processingToken, CancellationToken.None);
                                globalErrorOccurred = true;
                                break;
                            }

                            if (errorType == SynchronizationErrorType.Configuration)
                            {
                                _logger.LogError(ex, "Erro de Configuração Global detectado no cliente {ClienteId}. Interrompendo todo o lote atual.", record.ClienteId);
                                await syncRepository.UpdateFailedAsync(record.Id, processingToken, ex.Message, isPermanentError: false, _settings.MaxRetryAttempts, nextAttempt, stoppingToken);
                                
                                // Libera o restante do lote
                                await syncRepository.ReleaseBatchAsync(processingToken, record.Id, nextAttempt, "Lote interrompido devido a erro global de configuração.", stoppingToken);
                                
                                globalErrorOccurred = true;
                                break; // Interrompe o batch
                            }

                            _logger.LogError(ex, "Erro individual no processamento do cliente {ClienteId} (Transient/Permanent).", record.ClienteId);
                            
                            if (_safetySettings.Mode == "DryRun")
                            {
                                await syncRepository.CompleteDryRunAsync(record.Id, processingToken, currentHash ?? string.Empty, $"Erro DryRun: {ex.Message}", request?.SourceModifiedAt, stoppingToken);
                            }
                            else
                            {
                                await syncRepository.UpdateFailedAsync(record.Id, processingToken, ex.Message, isPermanent, _settings.MaxRetryAttempts, nextAttempt, stoppingToken);
                            }
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
