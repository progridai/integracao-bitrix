using Npgsql;
using System.Data;

namespace WebApolice.BitrixIntegration.Infrastructure.Database;

public class DbConnectionFactory
{
    private readonly string _connectionString;

    public DbConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public System.Data.Common.DbConnection CreateConnection()
    {
        return new NpgsqlConnection(_connectionString);
    }
}
