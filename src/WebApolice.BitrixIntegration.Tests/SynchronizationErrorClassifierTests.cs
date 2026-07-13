using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using WebApolice.BitrixIntegration.Modules.Bitrix;
using WebApolice.BitrixIntegration.Modules.Integracao.Services;
using Xunit;

namespace WebApolice.BitrixIntegration.Tests;

public class SynchronizationErrorClassifierTests
{
    [Fact]
    public void Classify_WhenTaskCanceledException_ReturnsCancelled()
    {
        var ex = new TaskCanceledException();
        var type = SynchronizationErrorClassifier.Classify(ex);
        Assert.Equal(SynchronizationErrorType.Cancelled, type);
    }

    [Fact]
    public void Classify_WhenConfigurationError_ReturnsConfiguration()
    {
        var ex = new BitrixException("auth error", "ERROR_OAUTH", "auth error", HttpStatusCode.Unauthorized, "method", "id");
        var type = SynchronizationErrorClassifier.Classify(ex);
        Assert.Equal(SynchronizationErrorType.Configuration, type);
    }

    [Fact]
    public void Classify_WhenRateLimitError_ReturnsTransient()
    {
        var ex = new BitrixException("rate limit", "TooManyRequests", "Too Many Requests", HttpStatusCode.TooManyRequests, "method", "id");
        var type = SynchronizationErrorClassifier.Classify(ex);
        Assert.Equal(SynchronizationErrorType.Transient, type);
    }

    [Fact]
    public void Classify_WhenMissingConfig_ReturnsConfiguration()
    {
        var ex = new InvalidOperationException("O campo customizado ExternalCustomerIdField no est configurado.");
        var type = SynchronizationErrorClassifier.Classify(ex);
        Assert.Equal(SynchronizationErrorType.Configuration, type);
    }

    [Fact]
    public void Classify_WhenBadRequest_ReturnsPermanent()
    {
        var ex = new BitrixException("bad request", "bad_request", "invalid fields", HttpStatusCode.BadRequest, "method", "id");
        var type = SynchronizationErrorClassifier.Classify(ex);
        Assert.Equal(SynchronizationErrorType.Permanent, type);
    }

    [Fact]
    public void Classify_WhenHttpRequestException_ReturnsTransient()
    {
        var ex = new HttpRequestException("Network failure");
        var type = SynchronizationErrorClassifier.Classify(ex);
        Assert.Equal(SynchronizationErrorType.Transient, type);
    }
}
