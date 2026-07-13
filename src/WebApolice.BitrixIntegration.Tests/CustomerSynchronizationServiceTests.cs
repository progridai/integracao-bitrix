using Microsoft.Extensions.Logging;
using Moq;
using System.Threading;
using System.Threading.Tasks;
using WebApolice.BitrixIntegration.Modules.Crm;
using WebApolice.BitrixIntegration.Modules.Integracao.Services;
using Xunit;

namespace WebApolice.BitrixIntegration.Tests;

public class CustomerSynchronizationServiceTests
{
    private readonly Mock<ICustomerCrmProvider> _crmProviderMock;
    private readonly Mock<ILogger<CustomerSynchronizationService>> _loggerMock;
    private readonly CustomerSynchronizationService _service;

    public CustomerSynchronizationServiceTests()
    {
        _crmProviderMock = new Mock<ICustomerCrmProvider>();
        _loggerMock = new Mock<ILogger<CustomerSynchronizationService>>();
        _service = new CustomerSynchronizationService(_crmProviderMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task SynchronizeCustomerAsync_ShouldCallUpsertAndReturnResult()
    {
        // Arrange
        var request = new CrmCustomerUpsertRequest { ExternalCustomerId = "123" };
        var expectedResult = new CrmCustomerUpsertResult { CrmId = "999", WasCreated = true };

        _crmProviderMock.Setup(p => p.UpsertCustomerAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _service.SynchronizeCustomerAsync(request, CancellationToken.None);

        // Assert
        Assert.Equal("999", result.CrmId);
        Assert.True(result.WasCreated);
        _crmProviderMock.Verify(p => p.UpsertCustomerAsync(request, It.IsAny<CancellationToken>()), Times.Once);
    }
}
