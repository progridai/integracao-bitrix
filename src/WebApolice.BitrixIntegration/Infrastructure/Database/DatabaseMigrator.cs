using DbUp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebApolice.BitrixIntegration.Modules.WebApolice;
using WebApolice.BitrixIntegration.Modules.Integracao;
using System.Reflection;
using System;

namespace WebApolice.BitrixIntegration.Infrastructure.Database;

public class DatabaseMigrator
{
    private readonly ILogger<DatabaseMigrator> _logger;
    private readonly WebApoliceDatabaseSettings _dbSettings;
    private readonly IntegrationDatabaseSettings _integrationDbSettings;

    public DatabaseMigrator(
        ILogger<DatabaseMigrator> logger,
        IOptions<WebApoliceDatabaseSettings> dbSettings,
        IOptions<IntegrationDatabaseSettings> integrationDbSettings)
    {
        _logger = logger;
        _dbSettings = dbSettings.Value;
        _integrationDbSettings = integrationDbSettings.Value;
    }

    public void Migrate()
    {
        if (!_integrationDbSettings.RunMigrationsOnStartup)
        {
            _logger.LogInformation("Migrations de banco de dados desabilitadas no startup.");
            return;
        }

        _logger.LogInformation("Iniciando execuo de migrations via DbUp...");

        try
        {
            EnsureDatabase.For.PostgresqlDatabase(_dbSettings.ConnectionString);

            var upgrader =
                DeployChanges.To
                    .PostgresqlDatabase(_dbSettings.ConnectionString)
                    .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly(), s => s.Contains("Migrations"))
                    .LogToConsole()
                    .Build();

            var result = upgrader.PerformUpgrade();

            if (!result.Successful)
            {
                _logger.LogError(result.Error, "Erro ao executar migrations.");
                throw new Exception("Falha na execuo de migrations. Verifique os logs.", result.Error);
            }

            var scriptsList = result.Scripts as System.Collections.Generic.List<DbUp.Engine.SqlScript> ?? new System.Collections.Generic.List<DbUp.Engine.SqlScript>(result.Scripts);
            _logger.LogInformation("Migrations executadas com sucesso. Scripts rodados: {Count}", scriptsList.Count);
            foreach (var script in scriptsList)
            {
                _logger.LogInformation(" - {ScriptName}", script.Name);
            }
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Erro fatal no processo de migration.");
            throw;
        }
    }
}
