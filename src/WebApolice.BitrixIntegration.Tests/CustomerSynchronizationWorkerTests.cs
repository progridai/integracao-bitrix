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
using Xunit;

namespace WebApolice.BitrixIntegration.Tests;

public class CustomerSynchronizationWorkerTests
{
    private readonly Mock<ILogger<CustomerSynchronizationWorker>> _loggerMock;
    private readonly Mock<CustomerSyncRepository> _syncRepoMock;
    private readonly Mock<WebApoliceCustomerRepository> _customerRepoMock;
    private readonly Mock<CustomerSynchronizationService> _syncServiceMock;
    private readonly CustomerSynchronizationSettings _settings;
    private readonly CustomerSynchronizationWorkerState _workerState;

    public CustomerSynchronizationWorkerTests()
    {
        _loggerMock = new Mock<ILogger<CustomerSynchronizationWorker>>();
        
        // Setup DbFactory mock just to satisfy dependencies
        var dbFactoryMock = new Mock<DbConnectionFactory>("Host=localhost");
        
        _syncRepoMock = new Mock<CustomerSyncRepository>(dbFactoryMock.Object, new Mock<ILogger<CustomerSyncRepository>>().Object);
        _customerRepoMock = new Mock<WebApoliceCustomerRepository>(dbFactoryMock.Object);
        
        var providerMock = new Mock<ICustomerCrmProvider>();
        _syncServiceMock = new Mock<CustomerSynchronizationService>(providerMock.Object, new Mock<ILogger<CustomerSynchronizationService>>().Object);
        
        _settings = new CustomerSynchronizationSettings { Enabled = true, BatchSize = 10, PollingIntervalSeconds = 5 };
        _workerState = new CustomerSynchronizationWorkerState();
    }

    private CustomerSynchronizationWorker CreateWorker()
    {
        return new CustomerSynchronizationWorker(
            _loggerMock.Object,
            Options.Create(_settings),
            _syncRepoMock.Object,
            _customerRepoMock.Object,
            _syncServiceMock.Object,
            _workerState
        );
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
        _syncRepoMock.Verify(x => x.ReserveBatchAsync(It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
