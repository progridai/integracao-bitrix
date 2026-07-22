using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WebApolice.BitrixIntegration.Infrastructure.Database;
using WebApolice.BitrixIntegration.Modules.Bitrix;
using WebApolice.BitrixIntegration.Modules.Crm;
using WebApolice.BitrixIntegration.Modules.Integracao;
using WebApolice.BitrixIntegration.Modules.Integracao.Repositories;
using WebApolice.BitrixIntegration.Modules.Integracao.Services;
using WebApolice.BitrixIntegration.Modules.Integracao.Workers;
using WebApolice.BitrixIntegration.Workers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace WebApolice.BitrixIntegration.Tests;

public class CustomerSynchronizationWorkerTests
{
    private readonly Mock<ILogger<CustomerSynchronizationWorker>> _loggerMock;
    private readonly Mock<CustomerSyncRepository> _syncRepoMock;
    private readonly Mock<WebApoliceCustomerRepository> _customerRepoMock;
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock;
    private readonly Mock<IServiceScope> _scopeMock;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<IOptions<CustomerSynchronizationSettings>> _settingsMock;
    private readonly Mock<IOptions<SynchronizationSafetySettings>> _safetySettingsMock;
    private readonly CustomerSynchronizationSettings _settings;
    private readonly CustomerSynchronizationWorkerState _workerState;
    private readonly CustomerSynchronizationWorker _worker;

    public CustomerSynchronizationWorkerTests()
    {
        _loggerMock = new Mock<ILogger<CustomerSynchronizationWorker>>();
        
        // Setup DbFactory mock just to satisfy dependencies
        var dbFactoryMock = new Mock<DbConnectionFactory>("Host=localhost");
        
        _syncRepoMock = new Mock<CustomerSyncRepository>(dbFactoryMock.Object, new Mock<ILogger<CustomerSyncRepository>>().Object);
        _customerRepoMock = new Mock<WebApoliceCustomerRepository>(dbFactoryMock.Object);
        
        _settings = new CustomerSynchronizationSettings { Enabled = true, BatchSize = 10, PollingIntervalSeconds = 5 };
        _workerState = new CustomerSynchronizationWorkerState();
        _settingsMock = new Mock<IOptions<CustomerSynchronizationSettings>>();
        _settingsMock.Setup(x => x.Value).Returns(_settings);
        _safetySettingsMock = new Mock<IOptions<SynchronizationSafetySettings>>();
        _safetySettingsMock.Setup(x => x.Value).Returns(new SynchronizationSafetySettings { Mode = "Live" });

        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _scopeMock = new Mock<IServiceScope>();
        _serviceProviderMock = new Mock<IServiceProvider>();

        _scopeFactoryMock.Setup(s => s.CreateScope()).Returns(_scopeMock.Object);
        _scopeMock.Setup(s => s.ServiceProvider).Returns(_serviceProviderMock.Object);

        _serviceProviderMock.Setup(s => s.GetService(typeof(CustomerSyncRepository))).Returns(_syncRepoMock.Object);
        _serviceProviderMock.Setup(s => s.GetService(typeof(WebApoliceCustomerRepository))).Returns(_customerRepoMock.Object);
        var syncServiceMock = new Mock<CustomerSynchronizationService>(new Mock<ICustomerCrmProvider>().Object, new Mock<Microsoft.Extensions.Logging.ILogger<CustomerSynchronizationService>>().Object);
        _serviceProviderMock.Setup(s => s.GetService(typeof(CustomerSynchronizationService))).Returns(syncServiceMock.Object);
        
        var validatorMock = new Mock<WebApolice.BitrixIntegration.Modules.Bitrix.IBitrixConfigurationValidator>();
        validatorMock.Setup(v => v.ValidateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<string>());
        _serviceProviderMock.Setup(s => s.GetService(typeof(WebApolice.BitrixIntegration.Modules.Bitrix.IBitrixConfigurationValidator))).Returns(validatorMock.Object);
        _serviceProviderMock.Setup(s => s.GetService(typeof(ICustomerCrmProvider))).Returns(new Mock<ICustomerCrmProvider>().Object);

        _worker = new CustomerSynchronizationWorker(
            _loggerMock.Object,
            _settingsMock.Object,
            _scopeFactoryMock.Object,
            _workerState,
            _safetySettingsMock.Object);
    }

    private CustomerSynchronizationWorker CreateWorker()
    {
        return new CustomerSynchronizationWorker(
            _loggerMock.Object,
            _settingsMock.Object,
            _scopeFactoryMock.Object,
            _workerState,
            _safetySettingsMock.Object);
    }

    [Fact]
    public async Task Worker_WhenDisabled_ShouldNotRun()
    {
        _settings.Enabled = false;
        var worker = CreateWorker();

        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(100);
        await worker.StopAsync(CancellationToken.None);

        Assert.False(_workerState.IsRunning);
        _syncRepoMock.Setup(x => x.ReserveBatchAsync(
                It.IsAny<int>(),
                It.IsAny<Guid>(),
                It.IsAny<bool>(),
                It.IsAny<List<long>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CustomerSyncRecord>());
    }

    [Fact]
    public async Task Worker_DryRun_ShouldCompleteDryRun_AndNotRepeatSameHash()
    {
        // 1. Setup
        _settings.Enabled = true;
        _safetySettingsMock.Setup(s => s.Value).Returns(new SynchronizationSafetySettings { Mode = "DryRun" });

        var record = new CustomerSyncRecord
        {
            Id = 1,
            ClienteId = 10,
            PessoaId = 20,
            Status = "PENDENTE"
        };
        
        var request = new CrmCustomerUpsertRequest 
        { 
            ExternalCustomerId = "10", 
            Name = "Teste DryRun" 
        };
        var currentHash = CustomerPayloadHasher.ComputeHash(request);

        _syncRepoMock.SetupSequence(x => x.ReserveBatchAsync(
                It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<List<long>>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CustomerSyncRecord> { record }) // Primeira vez: retorna o registro
            .ReturnsAsync(new List<CustomerSyncRecord>()); // Segunda vez: vazio para parar

        _customerRepoMock.Setup(x => x.GetCustomerUpsertRequestAsync(10, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        var syncServiceMock = Mock.Get(_serviceProviderMock.Object.GetService(typeof(CustomerSynchronizationService)) as CustomerSynchronizationService);
        syncServiceMock.Setup(x => x.SynchronizeCustomerAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CrmCustomerUpsertResult { CrmId = "VIRTUAL_123", WasCreated = true });

        var worker = CreateWorker();

        // 2. Act
        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(200); // Aguarda um ciclo
        await worker.StopAsync(CancellationToken.None);

        // 3. Assert
        _syncRepoMock.Verify(x => x.CompleteDryRunAsync(1, It.IsAny<Guid>(), currentHash, It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Once);
        syncServiceMock.Verify(x => x.SynchronizeCustomerAsync(request, It.IsAny<CancellationToken>()), Times.Once);

        // 4. Testar a NO-REPETIO do mesmo hash no DryRun
        record.LastDryRunHash = currentHash; // Simulando que foi salvo no banco
        
        _syncRepoMock.SetupSequence(x => x.ReserveBatchAsync(
                It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<List<long>>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CustomerSyncRecord> { record }) 
            .ReturnsAsync(new List<CustomerSyncRecord>()); 
            
        syncServiceMock.Invocations.Clear();
        _syncRepoMock.Invocations.Clear();

        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(200);
        await worker.StopAsync(CancellationToken.None);

        // Como o hash é o mesmo (LastDryRunHash == currentHash), o serviço de sync não deve ser chamado
        syncServiceMock.Verify(x => x.SynchronizeCustomerAsync(request, It.IsAny<CancellationToken>()), Times.Never);
        _syncRepoMock.Verify(x => x.UpdateSkippedAsync(1, It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Worker_Live_ShouldVerifyAfterWrite_AndPersistBitrixIdFirst()
    {
        // 1. Setup
        _settings.Enabled = true;
        _safetySettingsMock.Setup(s => s.Value).Returns(new SynchronizationSafetySettings { Mode = "Live", VerifyAfterWrite = true });

        var record = new CustomerSyncRecord
        {
            Id = 1, ClienteId = 10, PessoaId = 20, Status = "PENDENTE"
        };
        var request = new CrmCustomerUpsertRequest { ExternalCustomerId = "10", Name = "Teste Live", CustomerType = CrmCustomerType.Individual };
        var currentHash = CustomerPayloadHasher.ComputeHash(request);

        _syncRepoMock.SetupSequence(x => x.ReserveBatchAsync(
                It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<List<long>>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CustomerSyncRecord> { record })
            .ReturnsAsync(new List<CustomerSyncRecord>());

        _customerRepoMock.Setup(x => x.GetCustomerUpsertRequestAsync(10, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        var syncServiceMock = Mock.Get(_serviceProviderMock.Object.GetService(typeof(CustomerSynchronizationService)) as CustomerSynchronizationService);
        syncServiceMock.Setup(x => x.SynchronizeCustomerAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CrmCustomerUpsertResult { CrmId = "100", WasCreated = true });

        var crmProviderMock = Mock.Get(_serviceProviderMock.Object.GetService(typeof(ICustomerCrmProvider)) as ICustomerCrmProvider);
        
        // Simula falha na validação pós-escrita
        crmProviderMock.Setup(x => x.GetAvailableFieldsAsync(request.CustomerType, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Bitrix timeout on VerifyAfterWrite"));

        var worker = CreateWorker();

        // 2. Act
        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(200);
        await worker.StopAsync(CancellationToken.None);

        // 3. Assert
        // O ID foi persistido ANTES de falhar a validação
        _syncRepoMock.Verify(x => x.UpdateBitrixIdAsync(1, It.IsAny<Guid>(), "Individual", "100", It.IsAny<CancellationToken>()), Times.Once);
        // Foi registrado como erro (pois a validação falhou e lançou exceção)
        _syncRepoMock.Verify(x => x.UpdateFailedAsync(1, It.IsAny<Guid>(), It.IsAny<string>(), false, It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        // No atualizou sucesso porque falhou na metade
        _syncRepoMock.Verify(x => x.UpdateSuccessAsync(1, It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Worker_Live_ShouldProcessWhenTransitioningFromDryRun()
    {
        // 1. Setup
        _settings.Enabled = true;
        _safetySettingsMock.Setup(s => s.Value).Returns(new SynchronizationSafetySettings { Mode = "Live", VerifyAfterWrite = false });

        var request = new CrmCustomerUpsertRequest { ExternalCustomerId = "10", Name = "Teste Live Transition" };
        var currentHash = CustomerPayloadHasher.ComputeHash(request);

        // Registro que ja passou pelo DryRun com o mesmo hash
        var record = new CustomerSyncRecord
        {
            Id = 1, ClienteId = 10, PessoaId = 20, Status = "PENDENTE",
            LastDryRunHash = currentHash 
        };

        _syncRepoMock.SetupSequence(x => x.ReserveBatchAsync(
                It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<List<long>>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CustomerSyncRecord> { record })
            .ReturnsAsync(new List<CustomerSyncRecord>());

        _customerRepoMock.Setup(x => x.GetCustomerUpsertRequestAsync(10, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        var syncServiceMock = Mock.Get(_serviceProviderMock.Object.GetService(typeof(CustomerSynchronizationService)) as CustomerSynchronizationService);
        syncServiceMock.Setup(x => x.SynchronizeCustomerAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CrmCustomerUpsertResult { CrmId = "200", WasCreated = true });

        var worker = CreateWorker();

        // 2. Act
        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(200);
        await worker.StopAsync(CancellationToken.None);

        // 3. Assert
        // Mesmo que o LastDryRunHash seja igual ao hash atual, no modo Live ele deve processar.
        syncServiceMock.Verify(x => x.SynchronizeCustomerAsync(request, It.IsAny<CancellationToken>()), Times.Once);
        _syncRepoMock.Verify(x => x.UpdateSuccessAsync(1, It.IsAny<Guid>(), It.IsAny<string>(), "200", currentHash, It.IsAny<CancellationToken>()), Times.Once);
    }
}
