using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WebApolice.BitrixIntegration.Infrastructure.Database;
using WebApolice.BitrixIntegration.Infrastructure.Security;
using WebApolice.BitrixIntegration.Modules.Admin;
using WebApolice.BitrixIntegration.Modules.Bitrix;
using WebApolice.BitrixIntegration.Modules.Crm;
using WebApolice.BitrixIntegration.Modules.Integracao;
using WebApolice.BitrixIntegration.Modules.WebApolice;
using WebApolice.BitrixIntegration.Workers;

var builder = WebApplication.CreateBuilder(args);

// Settings
builder.Services.Configure<IntegrationDatabaseSettings>(builder.Configuration.GetSection("Integration:Database"));
builder.Services.Configure<BitrixSettings>(builder.Configuration.GetSection("Integration:Bitrix"));
builder.Services.Configure<WebApoliceDatabaseSettings>(builder.Configuration.GetSection("Integration:WebApoliceDatabase"));
builder.Services.Configure<AdminSettings>(builder.Configuration.GetSection("Integration:Admin"));

// Database Migrator
builder.Services.AddScoped<DatabaseMigrator>();
builder.Services.AddSingleton(sp => new WebApolice.BitrixIntegration.Infrastructure.Database.DbConnectionFactory(
    builder.Configuration.GetSection("Integration:WebApoliceDatabase:ConnectionString").Value ?? string.Empty));

// Bitrix HttpClient
builder.Services.AddHttpClient<WebApolice.BitrixIntegration.Modules.Bitrix.BitrixClient>((sp, client) =>
{
    // timeout is handled inside BitrixClient constructor
})
.ConfigurePrimaryHttpMessageHandler(sp =>
{
    var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<BitrixSettings>>().Value;
    var handler = new System.Net.Http.HttpClientHandler();
    if (settings.IgnoreSslValidation)
    {
        handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
        var logger = sp.GetRequiredService<ILogger<Program>>();
        logger.LogWarning("SSL Validation is IGNORED for Bitrix HttpClient. DO NOT USE IN PRODUCTION.");
    }
    return handler;
});

// Bitrix Services
builder.Services.AddScoped<WebApolice.BitrixIntegration.Modules.Bitrix.Services.BitrixProfileService>();
builder.Services.AddScoped<WebApolice.BitrixIntegration.Modules.Bitrix.Services.BitrixContactService>();
builder.Services.AddScoped<WebApolice.BitrixIntegration.Modules.Bitrix.Services.BitrixCompanyService>();

// Worker State and Settings
builder.Services.Configure<WebApolice.BitrixIntegration.Modules.Integracao.CustomerSynchronizationSettings>(
    builder.Configuration.GetSection("Integration:CustomerSynchronization"));
builder.Services.AddSingleton<WebApolice.BitrixIntegration.Modules.Integracao.Workers.CustomerSynchronizationWorkerState>();

builder.Services.Configure<WebApolice.BitrixIntegration.Modules.Integracao.CustomerDiscoverySettings>(
    builder.Configuration.GetSection("Integration:CustomerDiscovery"));
builder.Services.AddSingleton<WebApolice.BitrixIntegration.Modules.Integracao.Workers.CustomerDiscoveryWorkerState>();

// Worker Repositories
builder.Services.AddScoped<WebApolice.BitrixIntegration.Modules.Integracao.Repositories.CustomerSyncRepository>();
builder.Services.AddScoped<WebApolice.BitrixIntegration.Modules.Integracao.Repositories.WebApoliceCustomerRepository>();
builder.Services.AddScoped<WebApolice.BitrixIntegration.Modules.Integracao.Repositories.IWebApoliceCustomerSource, WebApolice.BitrixIntegration.Modules.Integracao.Repositories.WebApoliceCustomerSource>();

// CRM Provider & Sincronizao
builder.Services.AddScoped<ICustomerCrmProvider, WebApolice.BitrixIntegration.Modules.Bitrix.BitrixCustomerCrmProvider>();
builder.Services.AddScoped<WebApolice.BitrixIntegration.Modules.Integracao.Services.CustomerSynchronizationService>();

// Workers
builder.Services.AddHostedService<WebApolice.BitrixIntegration.Workers.CustomerSynchronizationWorker>();
builder.Services.AddHostedService<WebApolice.BitrixIntegration.Modules.Integracao.Workers.WebApoliceCustomerDiscoveryWorker>();

// Health Checks
builder.Services.AddHealthChecks()
    .AddCheck("Live", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Aplicativo rodando."))
    .AddCheck<WebApolice.BitrixIntegration.Api.Health.SyncWorkerHealthCheck>("Ready");

// Controllers
builder.Services.AddControllers();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Executar Migrations
using (var scope = app.Services.CreateScope())
{
    var migrator = scope.ServiceProvider.GetRequiredService<DatabaseMigrator>();
    migrator.Migrate();
}

// Swagger UI
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Middlewares
app.UseMiddleware<AdminApiKeyMiddleware>();

app.UseRouting();
app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = r => r.Name.Contains("Live")
});

app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = r => r.Name.Contains("Ready")
});

app.Run();
