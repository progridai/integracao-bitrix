using Dapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WebApolice.BitrixIntegration.Infrastructure.Database;

namespace WebApolice.BitrixIntegration.Modules.Integracao.Repositories;

public class WebApoliceCustomerSource : IWebApoliceCustomerSource
{
    private readonly DbConnectionFactory _dbFactory;

    public WebApoliceCustomerSource(DbConnectionFactory dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<CustomerDiscoveryCheckpoint?> GetCheckpointAsync(CancellationToken cancellationToken)
    {
        using var connection = _dbFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var sql = "SELECT * FROM integracao.sync_checkpoint WHERE process_name = 'WEBAPOLICE_CUSTOMER_DISCOVERY';";
        var result = await connection.QueryFirstOrDefaultAsync<dynamic>(new CommandDefinition(sql, cancellationToken: cancellationToken));

        if (result == null) return null;

        return new CustomerDiscoveryCheckpoint
        {
            ProcessName = result.process_name,
            LastModifiedAt = result.last_modified_at,
            LastEntityId = result.last_entity_id,
            LastStartedAt = result.last_started_at,
            LastFinishedAt = result.last_finished_at,
            LastSuccessAt = result.last_success_at,
            LastError = result.last_error,
            UpdatedAt = result.updated_at
        };
    }

    public async Task<CustomerDiscoveryCheckpoint> GetMaxCursorAsync(CancellationToken cancellationToken)
    {
        using var connection = _dbFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var sql = @"
            WITH customer_source AS
            (
                SELECT
                    c.id AS cliente_id,
                    GREATEST(c.updated_at, p.updated_at) AS source_modified_at
                FROM cadastro.cliente c
                INNER JOIN core.pessoa p
                    ON p.id = c.pessoa_id
                WHERE
                    c.deleted_at IS NULL
                    AND p.deleted_at IS NULL
            )
            SELECT
                source_modified_at AS LastModifiedAt,
                cliente_id AS LastEntityId
            FROM customer_source
            ORDER BY
                source_modified_at DESC,
                cliente_id DESC
            LIMIT 1;
        ";

        var result = await connection.QueryFirstOrDefaultAsync<CustomerDiscoveryCheckpoint>(new CommandDefinition(sql, cancellationToken: cancellationToken));

        if (result == null)
        {
            // Tabela vazia
            return new CustomerDiscoveryCheckpoint { LastModifiedAt = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc), LastEntityId = 0 };
        }

        return new CustomerDiscoveryCheckpoint
        {
            LastModifiedAt = result.LastModifiedAt,
            LastEntityId = result.LastEntityId
        };
    }

    public async Task ProcessBatchAsync(CustomerDiscoveryCheckpoint currentCheckpoint, int batchSize, CancellationToken cancellationToken)
    {
        using var connection = _dbFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        try
        {
            // 1. Ler o Lote
            var readSql = @"
                WITH customer_source AS
                (
                    SELECT
                        c.id AS cliente_id,
                        c.pessoa_id,
                        GREATEST(c.updated_at, p.updated_at) AS source_modified_at
                    FROM cadastro.cliente c
                    INNER JOIN core.pessoa p
                        ON p.id = c.pessoa_id
                    WHERE
                        c.deleted_at IS NULL
                        AND p.deleted_at IS NULL
                )
                SELECT
                    cliente_id AS ClienteId,
                    pessoa_id AS PessoaId,
                    source_modified_at AS SourceModifiedAt
                FROM customer_source
                WHERE
                    (source_modified_at, cliente_id) > (@LastModifiedAt, @LastEntityId)
                ORDER BY
                    source_modified_at ASC,
                    cliente_id ASC
                LIMIT @BatchSize;
            ";

            var batch = (await connection.QueryAsync<WebApoliceCustomerChange>(
                new CommandDefinition(readSql, new { LastModifiedAt = currentCheckpoint.LastModifiedAt, LastEntityId = currentCheckpoint.LastEntityId, BatchSize = batchSize }, transaction, cancellationToken: cancellationToken)
            )).ToList();

            if (!batch.Any())
            {
                // Nenhum dado novo, atualiza finished_at e sai
                var updateEmptySql = @"
                    UPDATE integracao.sync_checkpoint 
                    SET last_finished_at = NOW(), last_success_at = NOW(), last_error = NULL, updated_at = NOW()
                    WHERE process_name = 'WEBAPOLICE_CUSTOMER_DISCOVERY';
                ";
                await connection.ExecuteAsync(new CommandDefinition(updateEmptySql, transaction: transaction, cancellationToken: cancellationToken));
                transaction.Commit();
                return;
            }

            // 2. Realizar os Upserts
            var upsertSql = @"
                INSERT INTO integracao.bitrix_cliente_sync AS target (cliente_id, pessoa_id, status, source_modified_at)
                VALUES (@ClienteId, @PessoaId, 'PENDENTE', @SourceModifiedAt)
                ON CONFLICT (cliente_id) DO UPDATE SET 
                    pessoa_id = EXCLUDED.pessoa_id,
                    source_modified_at = EXCLUDED.source_modified_at,
                    status = CASE WHEN target.status = 'SINCRONIZANDO' THEN 'SINCRONIZANDO' ELSE 'PENDENTE' END,
                    tentativas = CASE WHEN target.status = 'SINCRONIZANDO' THEN target.tentativas ELSE 0 END,
                    next_attempt_at = CASE WHEN target.status = 'SINCRONIZANDO' THEN target.next_attempt_at ELSE NULL END,
                    ultimo_erro = CASE WHEN target.status = 'SINCRONIZANDO' THEN target.ultimo_erro ELSE NULL END
                WHERE target.source_modified_at IS NULL OR target.source_modified_at < EXCLUDED.source_modified_at;
            ";

            foreach (var item in batch)
            {
                await connection.ExecuteAsync(new CommandDefinition(upsertSql, new 
                { 
                    ClienteId = item.ClienteId, 
                    PessoaId = item.PessoaId, 
                    SourceModifiedAt = item.SourceModifiedAt 
                }, transaction, cancellationToken: cancellationToken));
            }

            // 3. Atualizar Checkpoint
            var lastItem = batch.Last();
            var updateCheckpointSql = @"
                INSERT INTO integracao.sync_checkpoint (process_name, last_modified_at, last_entity_id, last_started_at, last_finished_at, last_success_at, updated_at)
                VALUES ('WEBAPOLICE_CUSTOMER_DISCOVERY', @LastModifiedAt, @LastEntityId, NOW(), NOW(), NOW(), NOW())
                ON CONFLICT (process_name) DO UPDATE SET 
                    last_modified_at = @LastModifiedAt,
                    last_entity_id = @LastEntityId,
                    last_finished_at = NOW(),
                    last_success_at = NOW(),
                    last_error = NULL,
                    updated_at = NOW();
            ";

            await connection.ExecuteAsync(new CommandDefinition(updateCheckpointSql, new 
            { 
                LastModifiedAt = lastItem.SourceModifiedAt, 
                LastEntityId = lastItem.ClienteId 
            }, transaction, cancellationToken: cancellationToken));

            // 4. Confirmar transao
            transaction.Commit();
        }
        catch (Exception ex)
        {
            transaction.Rollback();

            // Tenta registrar o erro no checkpoint fora da transao principal
            try
            {
                var errorSql = @"
                    UPDATE integracao.sync_checkpoint 
                    SET last_finished_at = NOW(), last_error = @Error, updated_at = NOW()
                    WHERE process_name = 'WEBAPOLICE_CUSTOMER_DISCOVERY';
                ";
                await connection.ExecuteAsync(new CommandDefinition(errorSql, new { Error = ex.Message }, cancellationToken: CancellationToken.None));
            }
            catch { } // ignora erro secundrio de registro de log
            
            throw;
        }
    }

    public async Task StartCheckpointAsync(CancellationToken cancellationToken)
    {
        using var connection = _dbFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var startSql = @"
            UPDATE integracao.sync_checkpoint 
            SET last_started_at = NOW(), updated_at = NOW()
            WHERE process_name = 'WEBAPOLICE_CUSTOMER_DISCOVERY';
        ";
        await connection.ExecuteAsync(new CommandDefinition(startSql, cancellationToken: cancellationToken));
    }
    
    public async Task CreateInitialCheckpointAsync(DateTime lastModifiedAt, long lastEntityId, CancellationToken cancellationToken)
    {
        using var connection = _dbFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var sql = @"
            INSERT INTO integracao.sync_checkpoint (process_name, last_modified_at, last_entity_id, last_started_at, updated_at)
            VALUES ('WEBAPOLICE_CUSTOMER_DISCOVERY', @LastModifiedAt, @LastEntityId, NOW(), NOW())
            ON CONFLICT (process_name) DO UPDATE SET 
                last_modified_at = @LastModifiedAt,
                last_entity_id = @LastEntityId,
                updated_at = NOW();
        ";

        await connection.ExecuteAsync(new CommandDefinition(sql, new { LastModifiedAt = lastModifiedAt, LastEntityId = lastEntityId }, cancellationToken: cancellationToken));
    }
}
