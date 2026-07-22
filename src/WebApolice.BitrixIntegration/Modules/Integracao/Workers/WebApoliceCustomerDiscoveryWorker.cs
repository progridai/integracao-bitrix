using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using WebApolice.BitrixIntegration.Modules.Integracao.Repositories;

namespace WebApolice.BitrixIntegration.Modules.Integracao.Workers;

public class WebApoliceCustomerDiscoveryWorker : BackgroundService
{
    private readonly ILogger<WebApoliceCustomerDiscoveryWorker> _logger;
    private readonly CustomerDiscoverySettings _settings;
    private readonly IServiceProvider _serviceProvider;
    private readonly CustomerDiscoveryWorkerState _workerState;
    private readonly Channel<bool> _runOnceChannel;

    public WebApoliceCustomerDiscoveryWorker(
        ILogger<WebApoliceCustomerDiscoveryWorker> logger,
        IOptions<CustomerDiscoverySettings> settings,
        IServiceProvider serviceProvider,
        CustomerDiscoveryWorkerState workerState)
    {
        _logger = logger;
        _settings = settings.Value;
        _serviceProvider = serviceProvider;
        _workerState = workerState;
        
        // Channel para processar sinais run-once
        _runOnceChannel = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });
        
        ValidateSettings(_settings);
    }

    private void ValidateSettings(CustomerDiscoverySettings settings)
    {
        if (settings.PollingIntervalSeconds < 5)
            throw new ArgumentException("PollingIntervalSeconds deve ser pelo menos 5 segundos.");
        if (settings.BatchSize < 1 || settings.BatchSize > 5000)
            throw new ArgumentException("BatchSize deve estar entre 1 e 5000.");
    }

    public void RequestRunOnce()
    {
        _runOnceChannel.Writer.TryWrite(true);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.Enabled)
        {
            _workerState.IsRunning = false;
            _logger.LogInformation("WebApoliceCustomerDiscoveryWorker est desativado via config.");
            return;
        }

        _workerState.IsRunning = true;
        _logger.LogInformation("WebApoliceCustomerDiscoveryWorker iniciado. PollingInterval={PollingInterval}s", _settings.PollingIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Espera pelo prximo delay normal OU um sinal pelo Channel.
                // Criamos um token que cancela no timeout.
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                cts.CancelAfter(TimeSpan.FromSeconds(_settings.PollingIntervalSeconds));

                try
                {
                    await _runOnceChannel.Reader.ReadAsync(cts.Token);
                    _logger.LogInformation("Execuo do ciclo disparada via Run-Once.");
                }
                catch (OperationCanceledException)
                {
                    // Apenas o timeout do PollingInterval normal
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inexperado ao aguardar o prximo ciclo da descoberta.");
            }

            if (stoppingToken.IsCancellationRequested) break;

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var customerSource = scope.ServiceProvider.GetRequiredService<IWebApoliceCustomerSource>();

                _workerState.LastCycleStartedAt = DateTime.UtcNow;

                var checkpoint = await customerSource.GetCheckpointAsync(stoppingToken);

                if (checkpoint == null)
                {
                    _logger.LogInformation("Nenhum checkpoint encontrado. Inicializando...");
                    if (_settings.InitialLoadEnabled)
                    {
                        // Inicia da menor data possvel
                        await customerSource.CreateInitialCheckpointAsync(new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc), 0, stoppingToken);
                        checkpoint = await customerSource.GetCheckpointAsync(stoppingToken);
                    }
                    else
                    {
                        // Inicia do MAIOR cursor disponvel
                        var maxCursor = await customerSource.GetMaxCursorAsync(stoppingToken);
                        await customerSource.CreateInitialCheckpointAsync(maxCursor.LastModifiedAt, maxCursor.LastEntityId, stoppingToken);
                        checkpoint = await customerSource.GetCheckpointAsync(stoppingToken);
                        _logger.LogInformation("Carga inicial desabilitada. Checkpoint inicializado no cursor mximo: Data {Date}, ID {Id}", maxCursor.LastModifiedAt, maxCursor.LastEntityId);
                    }
                }

                if (checkpoint != null)
                {
                    // Ajuste o Checkpoint com a sobreposio, se necessrio
                    if (_settings.CheckpointOverlapSeconds > 0 && checkpoint.LastModifiedAt > new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc))
                    {
                        checkpoint.LastModifiedAt = checkpoint.LastModifiedAt.AddSeconds(-_settings.CheckpointOverlapSeconds);
                        checkpoint.LastEntityId = 0; // Quando recua a data, precisa resetar o ID para pegar todos daquela nova data.
                    }

                    int currentBatchSize = _settings.InitialLoadEnabled ? _settings.InitialLoadBatchSize : _settings.BatchSize;

                    // Marca incio
                    // (Na arquitetura atual, IWebApoliceCustomerSource faz Read -> Upsert -> Update Checkpoint tudo na mesma Transaction).
                    await customerSource.ProcessBatchAsync(checkpoint, currentBatchSize, stoppingToken);

                    // Atualiza State com novo Checkpoint
                    var newCheckpoint = await customerSource.GetCheckpointAsync(stoppingToken);
                    if (newCheckpoint != null)
                    {
                        _workerState.LastCheckpointAt = newCheckpoint.LastModifiedAt;
                        _workerState.LastCheckpointCustomerId = newCheckpoint.LastEntityId;
                        _workerState.LastSuccessfulCycleAt = DateTime.UtcNow;
                        _workerState.LastError = newCheckpoint.LastError;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("WebApoliceCustomerDiscoveryWorker interrompido via CancellationToken durante o ciclo.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro durante a descoberta de clientes.");
                _workerState.LastError = ex.Message;
            }
            finally
            {
                _workerState.LastCycleFinishedAt = DateTime.UtcNow;
            }
            
            // Se foi RunOnce, podemos aplicar um cooldown bsico ou ir direto para o prximo wait
        }

        _workerState.IsRunning = false;
        _logger.LogInformation("WebApoliceCustomerDiscoveryWorker finalizado.");
    }
}
