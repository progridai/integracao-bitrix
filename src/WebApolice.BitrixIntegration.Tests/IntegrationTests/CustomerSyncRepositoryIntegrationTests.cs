using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Testcontainers.PostgreSql;
using WebApolice.BitrixIntegration.Infrastructure.Database;
using WebApolice.BitrixIntegration.Modules.Integracao.Repositories;
using Xunit;
using Dapper;

namespace WebApolice.BitrixIntegration.Tests.IntegrationTests;

public class CustomerSyncRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer;
    private DbConnectionFactory _dbFactory = null!;
    private CustomerSyncRepository _syncRepository = null!;
    private WebApoliceCustomerRepository _webApoliceRepository = null!;

    public CustomerSyncRepositoryIntegrationTests()
    {
        _dbContainer = new PostgreSqlBuilder()
            .WithDatabase("testdb")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();
        
        var connectionString = _dbContainer.GetConnectionString();
        _dbFactory = new DbConnectionFactory(connectionString);
        
        _syncRepository = new CustomerSyncRepository(_dbFactory, NullLogger<CustomerSyncRepository>.Instance);
        _webApoliceRepository = new WebApoliceCustomerRepository(_dbFactory);

        // Run migrations/schema setup
        using var connection = _dbFactory.CreateConnection();
        await connection.OpenAsync();
        await connection.ExecuteAsync(@"
            CREATE SCHEMA IF NOT EXISTS cadastro;
            CREATE SCHEMA IF NOT EXISTS integracao;

            CREATE TABLE cadastro.cliente (
                id BIGSERIAL PRIMARY KEY,
                pessoa_id BIGINT NOT NULL,
                public_id UUID NULL,
                nome VARCHAR(100),
                documento_principal_limpo VARCHAR(14)
            );

            CREATE TABLE cadastro.contato (
                id BIGSERIAL PRIMARY KEY,
                pessoa_id BIGINT,
                tipo_contato_id INT,
                valor VARCHAR(100),
                inativo BOOLEAN DEFAULT FALSE,
                principal BOOLEAN DEFAULT FALSE
            );

            CREATE TABLE integracao.bitrix_cliente_sync (
                id BIGSERIAL PRIMARY KEY,
                cliente_id BIGINT,
                pessoa_id BIGINT,
                status VARCHAR(50),
                payload_hash VARCHAR(64),
                next_attempt_at TIMESTAMP WITH TIME ZONE,
                last_dry_run_hash VARCHAR(64),
                last_dry_run_at TIMESTAMP WITH TIME ZONE,
                last_dry_run_result TEXT,
                last_dry_run_source_modified_at TIMESTAMP WITH TIME ZONE,
                source_modified_at TIMESTAMP WITH TIME ZONE,
                processing_token UUID,
                processing_started_at TIMESTAMP WITH TIME ZONE
            );
        ");
    }

    public async Task DisposeAsync()
    {
        await _dbContainer.DisposeAsync();
    }

    [Fact]
    public async Task ReserveBatchAsync_ShouldApplyDryRunCondition_AndAvoidReservingAlreadyValidated()
    {
        using var connection = _dbFactory.CreateConnection();
        await connection.OpenAsync();

        var modifiedAt = DateTime.UtcNow.AddMinutes(-10);

        await connection.ExecuteAsync(@"
            INSERT INTO integracao.bitrix_cliente_sync (cliente_id, pessoa_id, status, source_modified_at, last_dry_run_source_modified_at)
            VALUES 
            (1, 1, 'PENDENTE', @ModifiedAt, @ModifiedAt), -- Should NOT be reserved (already validated)
            (2, 2, 'PENDENTE', @NewerModifiedAt, @ModifiedAt) -- Should be reserved (source modified after last dry run)
        ", new { ModifiedAt = modifiedAt, NewerModifiedAt = modifiedAt.AddMinutes(5) });

        var token = Guid.NewGuid();
        var reserved = await _syncRepository.ReserveBatchAsync(10, token, true, new List<long>(), true, CancellationToken.None);

        Assert.Single(reserved);
        Assert.Equal(2, reserved[0].ClienteId);
    }

    [Fact]
    public async Task GetInvalidPublicIdsCountAsync_ShouldReturnCount_WhenPublicIdIsNullOrDuplicate()
    {
        using var connection = _dbFactory.CreateConnection();
        await connection.OpenAsync();

        await connection.ExecuteAsync(@"
            INSERT INTO cadastro.cliente (id, pessoa_id, public_id) VALUES
            (1, 1, NULL),
            (2, 2, '00000000-0000-0000-0000-000000000000'),
            (3, 3, '11111111-1111-1111-1111-111111111111'),
            (4, 4, '11111111-1111-1111-1111-111111111111'),
            (5, 5, '22222222-2222-2222-2222-222222222222');

            INSERT INTO integracao.bitrix_cliente_sync (cliente_id, pessoa_id, status) VALUES
            (1, 1, 'PENDENTE'),
            (2, 2, 'PENDENTE'),
            (3, 3, 'PENDENTE'),
            (4, 4, 'PENDENTE'),
            (5, 5, 'PENDENTE');
        ");

        var count = await _webApoliceRepository.GetInvalidPublicIdsCountAsync(CancellationToken.None);
        
        // 1 NULL + 1 Empty + 2 Duplicates (3 and 4) = 4
        Assert.Equal(4, count);
    }
}
