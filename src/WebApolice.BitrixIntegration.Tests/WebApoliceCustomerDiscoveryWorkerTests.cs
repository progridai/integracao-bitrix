using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WebApolice.BitrixIntegration.Modules.Integracao;
using WebApolice.BitrixIntegration.Modules.Integracao.Repositories;
using WebApolice.BitrixIntegration.Modules.Integracao.Workers;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace WebApolice.BitrixIntegration.Tests;

public class WebApoliceCustomerDiscoveryWorkerTests
{
    private readonly Mock<IWebApoliceCustomerSource> _mockSource;
    private readonly CustomerDiscoveryWorkerState _state;
    private readonly CustomerDiscoverySettings _settings;
    
    public WebApoliceCustomerDiscoveryWorkerTests()
    {
        _mockSource = new Mock<IWebApoliceCustomerSource>();
        _state = new CustomerDiscoveryWorkerState();
        _settings = new CustomerDiscoverySettings
        {
            Enabled = true,
            PollingIntervalSeconds = 5, // Mnimo
            BatchSize = 100,
            InitialLoadEnabled = false
        };
    }

    [Fact]
    public async Task ExecuteAsync_WhenDisabled_ShouldNotRun()
    {
        _settings.Enabled = false;
        var mockServiceProvider = new Mock<IServiceProvider>();
        var mockServiceScope = new Mock<Microsoft.Extensions.DependencyInjection.IServiceScope>();
        var mockServiceScopeFactory = new Mock<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>();

        mockServiceProvider
            .Setup(x => x.GetService(typeof(Microsoft.Extensions.DependencyInjection.IServiceScopeFactory)))
            .Returns(mockServiceScopeFactory.Object);

        mockServiceScopeFactory
            .Setup(x => x.CreateScope())
            .Returns(mockServiceScope.Object);

        mockServiceScope
            .Setup(x => x.ServiceProvider)
            .Returns(mockServiceProvider.Object);

        mockServiceProvider
            .Setup(x => x.GetService(typeof(IWebApoliceCustomerSource)))
            .Returns(_mockSource.Object);

        var worker = new WebApoliceCustomerDiscoveryWorker(
            new NullLogger<WebApoliceCustomerDiscoveryWorker>(),
            Options.Create(_settings),
            mockServiceProvider.Object,
            _state);

        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(100);

        Assert.False(_state.IsRunning);
        _mockSource.Verify(x => x.GetCheckpointAsync(It.IsAny<CancellationToken>()), Times.Never);
        
        await worker.StopAsync(CancellationToken.None);
    }
    
    [Fact]
    public async Task ExecuteAsync_InitialLoadFalse_ShouldCreateCheckpointWithMaxCursor()
    {
        var mockServiceProvider = new Mock<IServiceProvider>();
        var mockServiceScope = new Mock<Microsoft.Extensions.DependencyInjection.IServiceScope>();
        var mockServiceScopeFactory = new Mock<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>();

        mockServiceProvider
            .Setup(x => x.GetService(typeof(Microsoft.Extensions.DependencyInjection.IServiceScopeFactory)))
            .Returns(mockServiceScopeFactory.Object);

        mockServiceScopeFactory
            .Setup(x => x.CreateScope())
            .Returns(mockServiceScope.Object);

        mockServiceScope
            .Setup(x => x.ServiceProvider)
            .Returns(mockServiceProvider.Object);

        mockServiceProvider
            .Setup(x => x.GetService(typeof(IWebApoliceCustomerSource)))
            .Returns(_mockSource.Object);

        var worker = new WebApoliceCustomerDiscoveryWorker(
            new NullLogger<WebApoliceCustomerDiscoveryWorker>(),
            Options.Create(_settings),
            mockServiceProvider.Object,
            _state);

        _mockSource.SetupSequence(x => x.GetCheckpointAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerDiscoveryCheckpoint?)null) // Primeira chamada nula
            .ReturnsAsync(new CustomerDiscoveryCheckpoint { LastModifiedAt = DateTime.UtcNow, LastEntityId = 100 }) // Aps criar
            .ReturnsAsync(new CustomerDiscoveryCheckpoint { LastModifiedAt = DateTime.UtcNow, LastEntityId = 100 });

        _mockSource.Setup(x => x.GetMaxCursorAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CustomerDiscoveryCheckpoint { LastModifiedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), LastEntityId = 999 });

        await worker.StartAsync(CancellationToken.None);
        worker.RequestRunOnce();
        await Task.Delay(200);

        _mockSource.Verify(x => x.GetMaxCursorAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockSource.Verify(x => x.CreateInitialCheckpointAsync(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), 999, It.IsAny<CancellationToken>()), Times.Once);
        _mockSource.Verify(x => x.ProcessBatchAsync(It.IsAny<CustomerDiscoveryCheckpoint>(), 100, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        
        await worker.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_InitialLoadTrue_ShouldCreateCheckpointFrom1900()
    {
        _settings.InitialLoadEnabled = true;
        var mockServiceProvider = new Mock<IServiceProvider>();
        var mockServiceScope = new Mock<Microsoft.Extensions.DependencyInjection.IServiceScope>();
        var mockServiceScopeFactory = new Mock<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>();

        mockServiceProvider
            .Setup(x => x.GetService(typeof(Microsoft.Extensions.DependencyInjection.IServiceScopeFactory)))
            .Returns(mockServiceScopeFactory.Object);

        mockServiceScopeFactory
            .Setup(x => x.CreateScope())
            .Returns(mockServiceScope.Object);

        mockServiceScope
            .Setup(x => x.ServiceProvider)
            .Returns(mockServiceProvider.Object);

        mockServiceProvider
            .Setup(x => x.GetService(typeof(IWebApoliceCustomerSource)))
            .Returns(_mockSource.Object);

        var worker = new WebApoliceCustomerDiscoveryWorker(
            new NullLogger<WebApoliceCustomerDiscoveryWorker>(),
            Options.Create(_settings),
            mockServiceProvider.Object,
            _state);

        _mockSource.SetupSequence(x => x.GetCheckpointAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerDiscoveryCheckpoint?)null) // Primeira nula
            .ReturnsAsync(new CustomerDiscoveryCheckpoint { LastModifiedAt = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc), LastEntityId = 0 })
            .ReturnsAsync(new CustomerDiscoveryCheckpoint { LastModifiedAt = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc), LastEntityId = 0 });

        await worker.StartAsync(CancellationToken.None);
        worker.RequestRunOnce();
        await Task.Delay(200);

        _mockSource.Verify(x => x.GetMaxCursorAsync(It.IsAny<CancellationToken>()), Times.Never);
        _mockSource.Verify(x => x.CreateInitialCheckpointAsync(new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc), 0, It.IsAny<CancellationToken>()), Times.Once);
        
        await worker.StopAsync(CancellationToken.None);
    }
}
