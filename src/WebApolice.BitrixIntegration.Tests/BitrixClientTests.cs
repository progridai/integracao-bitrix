using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RichardSzalay.MockHttp;
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WebApolice.BitrixIntegration.Infrastructure.Database;
using WebApolice.BitrixIntegration.Modules.Bitrix;
using WebApolice.BitrixIntegration.Modules.Bitrix.Dtos;
using Xunit;

namespace WebApolice.BitrixIntegration.Tests;

public class BitrixClientTests
{
    private readonly MockHttpMessageHandler _mockHttp;
    private readonly HttpClient _httpClient;
    private readonly IOptions<BitrixSettings> _settings;
    private readonly Mock<ILogger<BitrixClient>> _loggerMock;
    private readonly Mock<DbConnectionFactory> _dbFactoryMock;
    private readonly BitrixClient _client;

    public BitrixClientTests()
    {
        _mockHttp = new MockHttpMessageHandler();
        _httpClient = _mockHttp.ToHttpClient();
        
        _settings = Options.Create(new BitrixSettings
        {
            WebhookBaseUrl = "https://bitrix24.local/rest/1/abcxyz/"
        });
        
        _loggerMock = new Mock<ILogger<BitrixClient>>();
        
        // Mock DbConnectionFactory with null string so we don't need real DB connection for log test
        _dbFactoryMock = new Mock<DbConnectionFactory>("Host=localhost;Database=dummy");
        // To avoid errors during mock DB factory usage we just let it fail gracefully (the try-catch in LogToDatabaseAsync handles it)

        _client = new BitrixClient(_httpClient, _settings, _loggerMock.Object, _dbFactoryMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_WhenHttpError_ShouldThrowBitrixException()
    {
        _mockHttp.When("https://bitrix24.local/rest/1/abcxyz/crm.contact.add.json")
                 .Respond(HttpStatusCode.InternalServerError, "text/plain", "Server Error");

        var ex = await Assert.ThrowsAsync<BitrixException>(() => 
            _client.ExecuteAsync<BitrixResponse<int>>("crm.contact.add.json", new { }, CancellationToken.None));

        Assert.Equal(HttpStatusCode.InternalServerError, ex.HttpStatusCode);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRestErrorWithHttp200_ShouldThrowBitrixException()
    {
        _mockHttp.When("https://bitrix24.local/rest/1/abcxyz/crm.contact.get.json")
                 .Respond("application/json", "{\"error\":\"ERROR_METHOD_NOT_FOUND\",\"error_description\":\"Method not found\"}");

        var ex = await Assert.ThrowsAsync<BitrixException>(() => 
            _client.ExecuteAsync<BitrixResponse<BitrixContactDto>>("crm.contact.get.json", new { }, CancellationToken.None));

        Assert.Equal("ERROR_METHOD_NOT_FOUND", ex.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSuccess_ShouldReturnDeserializedObject()
    {
        _mockHttp.When("https://bitrix24.local/rest/1/abcxyz/profile.json")
                 .Respond("application/json", "{\"result\":{\"ID\":\"1\",\"NAME\":\"John\"}}");

        var result = await _client.ExecuteAsync<BitrixResponse<BitrixProfileDto>>("profile.json", null, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("1", result.Result?.Id);
        Assert.Equal("John", result.Result?.Name);
    }
}
