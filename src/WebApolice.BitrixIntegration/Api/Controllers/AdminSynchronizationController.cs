using Dapper;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;
using WebApolice.BitrixIntegration.Infrastructure.Database;
using WebApolice.BitrixIntegration.Modules.Integracao.Workers;

namespace WebApolice.BitrixIntegration.Api.Controllers;

[ApiController]
[Route("admin/synchronization")]
public class AdminSynchronizationController : ControllerBase
{
    private readonly DbConnectionFactory _dbFactory;
    private readonly CustomerSynchronizationWorkerState _workerState;

    public AdminSynchronizationController(DbConnectionFactory dbFactory, CustomerSynchronizationWorkerState workerState)
    {
        _dbFactory = dbFactory;
        _workerState = workerState;
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken cancellationToken)
    {
        using var connection = _dbFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var sql = @"
            SELECT 
                COUNT(*) FILTER (WHERE status = 'PENDENTE') as Pending,
                COUNT(*) FILTER (WHERE status = 'SINCRONIZANDO') as Processing,
                COUNT(*) FILTER (WHERE status = 'ERRO') as Failed,
                COUNT(*) FILTER (WHERE status = 'DEAD_LETTER') as DeadLetter,
                COUNT(*) FILTER (WHERE status = 'SINCRONIZADO' AND ultima_sincronizacao >= NOW() - INTERVAL '24 hours') as SuccessLast24Hours,
                COUNT(*) FILTER (WHERE status = 'ERRO' AND updated_at >= NOW() - INTERVAL '24 hours') as FailedLast24Hours
            FROM integracao.bitrix_cliente_sync;
        ";

        var counts = await connection.QueryFirstOrDefaultAsync(new CommandDefinition(sql, cancellationToken: cancellationToken));

        return Ok(new
        {
            workerRunning = _workerState.IsRunning,
            lastCycleStartedAt = _workerState.LastCycleStartedAt,
            lastCycleFinishedAt = _workerState.LastCycleFinishedAt,
            lastSuccessfulCycleAt = _workerState.LastSuccessfulCycleAt,
            lastError = _workerState.LastError,
            pending = counts?.pending ?? 0,
            processing = counts?.processing ?? 0,
            failed = counts?.failed ?? 0,
            deadLetter = counts?.deadletter ?? 0,
            successLast24Hours = counts?.successlast24hours ?? 0,
            failedLast24Hours = counts?.failedlast24hours ?? 0
        });
    }

    [HttpPost("dead-letter/retry")]
    public async Task<IActionResult> RetryDeadLetter([FromBody] RetryRequest request, CancellationToken cancellationToken)
    {
        int limit = request?.Limit ?? 100;
        if (limit > 1000) limit = 1000;

        using var connection = _dbFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var sql = @"
            WITH to_update AS (
                SELECT id FROM integracao.bitrix_cliente_sync
                WHERE status = 'DEAD_LETTER'
                ORDER BY updated_at ASC
                LIMIT @Limit
            )
            UPDATE integracao.bitrix_cliente_sync t
            SET status = 'PENDENTE',
                processing_token = NULL,
                processing_started_at = NULL,
                next_attempt_at = NULL,
                tentativas = 0
            FROM to_update
            WHERE t.id = to_update.id;
        ";

        var affectedRows = await connection.ExecuteAsync(new CommandDefinition(sql, new { Limit = limit }, cancellationToken: cancellationToken));

        return Ok(new { success = true, retriedCount = affectedRows });
    }

    [HttpPost("customers/{clienteId}/retry")]
    public async Task<IActionResult> RetryCustomer(long clienteId, CancellationToken cancellationToken)
    {
        using var connection = _dbFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var sql = @"
            UPDATE integracao.bitrix_cliente_sync
            SET status = 'PENDENTE',
                processing_token = NULL,
                processing_started_at = NULL,
                next_attempt_at = NULL,
                tentativas = 0
            WHERE cliente_id = @ClienteId;
        ";

        var affectedRows = await connection.ExecuteAsync(new CommandDefinition(sql, new { ClienteId = clienteId }, cancellationToken: cancellationToken));

        if (affectedRows == 0) return NotFound("Cliente no encontrado na fila de sincronizacao.");
        return Ok(new { success = true, message = "Cliente retornado para PENDENTE." });
    }
}

public class RetryRequest
{
    public int Limit { get; set; } = 100;
}
