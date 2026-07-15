using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;
using WebApolice.BitrixIntegration.Modules.Admin;

namespace WebApolice.BitrixIntegration.Infrastructure.Security;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireAdminApiKeyAttribute : Attribute, IAsyncActionFilter
{
    private const string API_KEY_HEADER = "X-Admin-Key";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.HttpContext.Request.Headers.TryGetValue(API_KEY_HEADER, out var potentialKey))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var settings = context.HttpContext.RequestServices.GetRequiredService<IOptions<AdminSettings>>().Value;

        if (string.IsNullOrWhiteSpace(settings.ApiKey) || !settings.ApiKey.Equals(potentialKey))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        await next();
    }
}
