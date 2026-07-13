using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;
using WebApolice.BitrixIntegration.Modules.Admin;

namespace WebApolice.BitrixIntegration.Infrastructure.Security;

public class AdminApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private const string ApiKeyHeaderName = "X-Admin-Key";

    public AdminApiKeyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IOptions<AdminSettings> adminSettings)
    {
        if (context.Request.Path.StartsWithSegments("/admin"))
        {
            if (!context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var extractedApiKey))
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("API Key no fornecida.");
                return;
            }

            var configuredApiKey = adminSettings.Value.ApiKey;

            if (string.IsNullOrWhiteSpace(configuredApiKey) || !configuredApiKey.Equals(extractedApiKey))
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("API Key invlida.");
                return;
            }
        }

        await _next(context);
    }
}
