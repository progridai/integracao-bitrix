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
    private readonly CustomerSynchronizationSettings _settings;
    private readonly CustomerSynchronizationWorkerState _workerState;

    public SyncWorkerHealthCheck(DbConnectionFactory dbFactory, IOptions<CustomerSynchronizationSettings> settings, CustomerSynchronizationWorkerState workerState)
    {
        _dbFactory = dbFactory;
        _settings = settings.Value;
        _workerState = workerState;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
        {
            return HealthCheckResult.Healthy("A sincronizao de clientes est desabilitada. O servio operante de forma saudvel.");
        }

        try
        {
            using var connection = _dbFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            // Basic ping to check if DB is accessible
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Falha ao conectar no banco de dados.", ex);
        }

        if (!_workerState.IsRunning)
        {
            return HealthCheckResult.Unhealthy("Worker est habilitado, mas no est rodando.");
        }

        if (_workerState.LastCycleStartedAt.HasValue && _workerState.LastCycleStartedAt.Value < DateTime.UtcNow.AddMinutes(-30))
        {
            // Worker travou e no inicia ciclos h 30 min.
            return HealthCheckResult.Unhealthy("O worker no inicia novos ciclos h mais de 30 minutos.");
        }

        return HealthCheckResult.Healthy("O worker de sincronizao est operando normalmente.");
    }
}
