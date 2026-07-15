using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;
using WebApolice.BitrixIntegration.Infrastructure.Database;
using WebApolice.BitrixIntegration.Modules.Integracao;
using WebApolice.BitrixIntegration.Modules.Integracao.Workers;

namespace WebApolice.BitrixIntegration.Api.Health;

public class SyncWorkerHealthCheck : IHealthCheck
{
    private readonly DbConnectionFactory _dbFactory;
    private readonly CustomerSynchronizationSettings _syncSettings;
    private readonly CustomerSynchronizationWorkerState _syncWorkerState;
    private readonly CustomerDiscoverySettings _discoverySettings;
    private readonly CustomerDiscoveryWorkerState _discoveryWorkerState;

    public SyncWorkerHealthCheck(
        DbConnectionFactory dbFactory, 
        IOptions<CustomerSynchronizationSettings> syncSettings, 
        CustomerSynchronizationWorkerState syncWorkerState,
        IOptions<CustomerDiscoverySettings> discoverySettings,
        CustomerDiscoveryWorkerState discoveryWorkerState)
    {
        _dbFactory = dbFactory;
        _syncSettings = syncSettings.Value;
        _syncWorkerState = syncWorkerState;
        _discoverySettings = discoverySettings.Value;
        _discoveryWorkerState = discoveryWorkerState;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = _dbFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            // Ping
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Falha ao conectar no banco de dados.", ex);
        }

        if (_syncSettings.Enabled)
        {
            if (!_syncWorkerState.IsRunning)
                return HealthCheckResult.Unhealthy("Sync Worker est habilitado, mas no est rodando.");

            if (_syncWorkerState.LastCycleStartedAt.HasValue && _syncWorkerState.LastCycleStartedAt.Value < DateTime.UtcNow.AddMinutes(-30))
                return HealthCheckResult.Unhealthy("O Sync worker no inicia novos ciclos h mais de 30 minutos.");
        }

        if (_discoverySettings.Enabled)
        {
            if (!_discoveryWorkerState.IsRunning)
                return HealthCheckResult.Unhealthy("Discovery Worker est habilitado, mas no est rodando.");

            if (_discoveryWorkerState.LastCycleStartedAt.HasValue && _discoveryWorkerState.LastCycleStartedAt.Value < DateTime.UtcNow.AddMinutes(-30))
                return HealthCheckResult.Unhealthy("O Discovery worker no inicia novos ciclos h mais de 30 minutos.");
        }

        return HealthCheckResult.Healthy("A integrao est operando normalmente.");
    }
}
