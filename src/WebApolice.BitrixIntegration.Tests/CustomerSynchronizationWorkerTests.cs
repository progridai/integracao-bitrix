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

        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _scopeMock = new Mock<IServiceScope>();
        _serviceProviderMock = new Mock<IServiceProvider>();

        _scopeFactoryMock.Setup(s => s.CreateScope()).Returns(_scopeMock.Object);
        _scopeMock.Setup(s => s.ServiceProvider).Returns(_serviceProviderMock.Object);

        _serviceProviderMock.Setup(s => s.GetService(typeof(CustomerSyncRepository))).Returns(_syncRepoMock.Object);
        _serviceProviderMock.Setup(s => s.GetService(typeof(WebApoliceCustomerRepository))).Returns(_customerRepoMock.Object);
        var syncServiceMock = new Mock<CustomerSynchronizationService>(_customerRepoMock.Object, new Mock<ICustomerCrmProvider>().Object, _syncRepoMock.Object, new Mock<Microsoft.Extensions.Logging.ILogger<CustomerSynchronizationService>>().Object);
        _serviceProviderMock.Setup(s => s.GetService(typeof(CustomerSynchronizationService))).Returns(syncServiceMock.Object);
        
        var validatorMock = new Mock<WebApolice.BitrixIntegration.Modules.Bitrix.BitrixConfigurationValidator>(new Mock<WebApolice.BitrixIntegration.Modules.Bitrix.Services.BitrixContactService>(null, null).Object, new Mock<WebApolice.BitrixIntegration.Modules.Bitrix.Services.BitrixCompanyService>(null, null).Object, Options.Create(new WebApolice.BitrixIntegration.Modules.Bitrix.BitrixSettings()));
        validatorMock.Setup(v => v.ValidateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<string>());
        _serviceProviderMock.Setup(s => s.GetService(typeof(WebApolice.BitrixIntegration.Modules.Bitrix.BitrixConfigurationValidator))).Returns(validatorMock.Object);
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
        return _worker;
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
}
