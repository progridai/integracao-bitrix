using Dapper;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WebApolice.BitrixIntegration.Infrastructure.Database;

namespace WebApolice.BitrixIntegration.Modules.Integracao.Repositories;

public class CustomerSyncRepository
{
    private readonly DbConnectionFactory _dbFactory;
    private readonly ILogger<CustomerSyncRepository> _logger;

    public CustomerSyncRepository(DbConnectionFactory dbFactory, ILogger<CustomerSyncRepository> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public virtual async Task<IReadOnlyList<CustomerSyncRecord>> ReserveBatchAsync(int batchSize, Guid processingToken, CancellationToken cancellationToken)
    {
        using var connection = _dbFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var sql = @"
            WITH reservable AS (
                SELECT id 
                FROM integracao.bitrix_cliente_sync
                WHERE status IN ('PENDENTE', 'ERRO')
                  AND (next_attempt_at IS NULL OR next_attempt_at <= NOW())
                ORDER BY id ASC
                LIMIT @BatchSize
                FOR UPDATE SKIP LOCKED
            )
            UPDATE integracao.bitrix_cliente_sync target
            SET status = 'SINCRONIZANDO',
                processing_token = @ProcessingToken,
                processing_started_at = NOW()
            FROM reservable
            WHERE target.id = reservable.id
            RETURNING target.*;
        ";

        var result = await connection.QueryAsync<CustomerSyncRecord>(
            new CommandDefinition(sql, new { BatchSize = batchSize, ProcessingToken = processingToken }, cancellationToken: cancellationToken));

        return result.ToList();
    }

    public virtual async Task ReleaseBatchAsync(Guid processingToken, long? exceptCurrentRecordId, DateTime nextAttemptAt, string errorMessage, CancellationToken cancellationToken)
    {
        using var connection = _dbFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var sql = @"
            UPDATE integracao.bitrix_cliente_sync
            SET status = 'ERRO',
                processing_token = NULL,
                processing_started_at = NULL,
                next_attempt_at = @NextAttemptAt,
                ultimo_erro = @ErrorMessage
            WHERE processing_token = @ProcessingToken
              AND status = 'SINCRONIZANDO'
              AND (@ExceptCurrentRecordId IS NULL OR id != @ExceptCurrentRecordId);
        ";

        await connection.ExecuteAsync(new CommandDefinition(sql, new 
        { 
            ProcessingToken = processingToken, 
            ExceptCurrentRecordId = exceptCurrentRecordId,
            NextAttemptAt = nextAttemptAt,
            ErrorMessage = errorMessage
        }, cancellationToken: cancellationToken));
    }

    public virtual async Task RecoverStuckRecordsAsync(int processingTimeoutMinutes, CancellationToken cancellationToken)
    {
        using var connection = _dbFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var sql = @"
            UPDATE integracao.bitrix_cliente_sync
            SET tentativas = tentativas + 1,
                status = CASE 
                            WHEN tentativas + 1 >= 5 THEN 'DEAD_LETTER' 
                            ELSE 'ERRO' 
                         END,
                next_attempt_at = NOW() + INTERVAL '1 minute',
                processing_token = NULL,
                processing_started_at = NULL,
                ultimo_erro = 'Processamento interrompido abruptamente ou travado por timeout.'
            WHERE status = 'SINCRONIZANDO'
              AND processing_started_at < NOW() - INTERVAL '1 minute' * @TimeoutMinutes;
        ";

        var affectedRows = await connection.ExecuteAsync(new CommandDefinition(sql, new { TimeoutMinutes = processingTimeoutMinutes }, cancellationToken: cancellationToken));
        if (affectedRows > 0)
        {
            _logger.LogWarning("Recuperados {AffectedRows} registros travados na fila de sincronizacao do Bitrix.", affectedRows);
        }
    }

    public virtual async Task UpdateSuccessAsync(long id, Guid processingToken, string bitrixEntityType, string bitrixId, string hash, CancellationToken cancellationToken)
    {
        using var connection = _dbFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var sql = @"
            UPDATE integracao.bitrix_cliente_sync
            SET status = 'SINCRONIZADO',
                bitrix_entity_type = @BitrixEntityType,
                bitrix_id = @BitrixId,
                payload_hash = @Hash,
                ultima_sincronizacao = NOW(),
                processing_token = NULL,
                processing_started_at = NULL,
                ultimo_erro = NULL,
                next_attempt_at = NULL,
                tentativas = 0
            WHERE id = @Id 
              AND processing_token = @ProcessingToken 
              AND status = 'SINCRONIZANDO';
        ";

        await connection.ExecuteAsync(new CommandDefinition(sql, new 
        { 
            Id = id, 
            ProcessingToken = processingToken,
            BitrixEntityType = bitrixEntityType,
            BitrixId = bitrixId,
            Hash = hash
        }, cancellationToken: cancellationToken));
    }

    public virtual async Task UpdateFailedAsync(long id, Guid processingToken, string errorMessage, bool isPermanentError, int maxRetries, DateTime nextAttemptAt, CancellationToken cancellationToken)
    {
        using var connection = _dbFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var sql = @"
            UPDATE integracao.bitrix_cliente_sync
            SET tentativas = tentativas + 1,
                status = CASE 
                            WHEN @IsPermanentError = true THEN 'DEAD_LETTER'
                            WHEN tentativas + 1 >= @MaxRetries THEN 'DEAD_LETTER'
                            ELSE 'ERRO'
                         END,
                next_attempt_at = @NextAttemptAt,
                ultimo_erro = @ErrorMessage,
                processing_token = NULL,
                processing_started_at = NULL
            WHERE id = @Id 
              AND processing_token = @ProcessingToken 
              AND status = 'SINCRONIZANDO';
        ";

        await connection.ExecuteAsync(new CommandDefinition(sql, new 
        { 
            Id = id, 
            ProcessingToken = processingToken,
            ErrorMessage = errorMessage,
            IsPermanentError = isPermanentError,
            MaxRetries = maxRetries,
            NextAttemptAt = nextAttemptAt
        }, cancellationToken: cancellationToken));
    }

    public virtual async Task UpdateSkippedAsync(long id, Guid processingToken, CancellationToken cancellationToken)
    {
        using var connection = _dbFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var sql = @"
            UPDATE integracao.bitrix_cliente_sync
            SET status = 'SINCRONIZADO',
                processing_token = NULL,
                processing_started_at = NULL,
                ultimo_erro = 'Ignorado - Nenhum dado alterado'
            WHERE id = @Id 
              AND processing_token = @ProcessingToken 
              AND status = 'SINCRONIZANDO';
        ";

        await connection.ExecuteAsync(new CommandDefinition(sql, new 
        { 
            Id = id, 
            ProcessingToken = processingToken
        }, cancellationToken: cancellationToken));
    }

    public virtual async Task ClearProcessingTokenAsync(long id, Guid processingToken, CancellationToken cancellationToken)
    {
        using var connection = _dbFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var sql = @"
            UPDATE integracao.bitrix_cliente_sync
            SET status = 'PENDENTE',
                processing_token = NULL,
                processing_started_at = NULL
            WHERE id = @Id 
              AND processing_token = @ProcessingToken 
              AND status = 'SINCRONIZANDO';
        ";

        await connection.ExecuteAsync(new CommandDefinition(sql, new 
        { 
            Id = id, 
            ProcessingToken = processingToken
        }, cancellationToken: cancellationToken));
    }
}
