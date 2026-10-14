using Npgsql;
using Testcontainers.PostgreSql;

namespace Taskify.Api.Tests.Infrastructure;

/// <summary>
/// One PostgreSQL 16 container per test process. Each test class creates its own
/// database on it. The Testcontainers resource reaper removes the container when
/// the process exits.
/// </summary>
public static class SharedPostgres
{
    private static readonly Lazy<Task<PostgreSqlContainer>> Container = new(StartAsync);

    public static async Task<string> CreateDatabaseAsync(string name)
    {
        var container = await Container.Value;
        await ExecuteAsync(container, $"CREATE DATABASE \"{name}\"");

        return new NpgsqlConnectionStringBuilder(container.GetConnectionString())
        {
            Database = name,
        }.ConnectionString;
    }

    public static async Task DropDatabaseAsync(string name)
    {
        var container = await Container.Value;
        await ExecuteAsync(container, $"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)");
    }

    private static async Task<PostgreSqlContainer> StartAsync()
    {
        var container = new PostgreSqlBuilder("postgres:16").Build();
        await container.StartAsync();
        return container;
    }

    private static async Task ExecuteAsync(PostgreSqlContainer container, string sql)
    {
        await using var connection = new NpgsqlConnection(container.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
