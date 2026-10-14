using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Taskify.Api.Tests.Infrastructure;

namespace Taskify.Api.Tests.Data;

/// <summary>
/// Only Taskify.Migrator writes the schema. Starting the API must not migrate (FR-006).
/// </summary>
public class SchemaOwnershipTests
{
    [Fact]
    public async Task ApiStartup_DoesNotApplyMigrations()
    {
        var name = $"taskify_empty_{Guid.NewGuid():N}";
        var connectionString = await SharedPostgres.CreateDatabaseAsync(name);
        try
        {
            await using (
                var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
                    b.UseTaskifyTestSettings(connectionString)
                )
            )
            {
                // Forces the host to start.
                factory.CreateClient().Dispose();
            }

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                "SELECT to_regclass('\"__EFMigrationsHistory\"') IS NOT NULL",
                connection
            );
            Assert.False((bool)(await command.ExecuteScalarAsync())!);
        }
        finally
        {
            await SharedPostgres.DropDatabaseAsync(name);
        }
    }
}
