using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Taskify.Api.Data;
using Taskify.Shared.Dtos;
using Taskify.Shared.Requests;

namespace Taskify.Api.Tests.Infrastructure;

/// <summary>
/// Hosts the API in memory against a fresh, migrated PostgreSQL database.
/// Use through <c>IClassFixture&lt;TaskifyApiFixture&gt;</c>: one database per test class.
/// </summary>
public sealed class TaskifyApiFixture : IAsyncLifetime
{
    private readonly string _databaseName = $"taskify_{Guid.NewGuid():N}";

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>The API's clock. It only moves when a test advances it.</summary>
    public FakeTimeProvider Time { get; } = new(DateTimeOffset.UtcNow);

    public async Task InitializeAsync()
    {
        ConnectionString = await SharedPostgres.CreateDatabaseAsync(_databaseName);

        await using (var db = CreateDbContext())
        {
            await db.Database.MigrateAsync();
        }

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder
                .UseTaskifyTestSettings(ConnectionString)
                .ConfigureTestServices(s => s.AddSingleton<TimeProvider>(Time))
        );
    }

    /// <summary>A client with no identity.</summary>
    public HttpClient CreateClient() => Factory.CreateClient();

    /// <summary>A client that acts as <paramref name="userId"/>, through the token endpoint.</summary>
    public async Task<HttpClient> CreateClientAsAsync(int userId)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await GetTokenAsync(userId)
        );
        return client;
    }

    public async Task<string> GetTokenAsync(int userId)
    {
        var response = await Factory
            .CreateClient()
            .PostAsJsonAsync("/api/auth/token", new TokenRequest(userId), TestJson.Options);
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(TestJson.Options);
        return token!.AccessToken;
    }

    public TaskifyDbContext CreateDbContext() =>
        new(
            new DbContextOptionsBuilder<TaskifyDbContext>()
                .UseNpgsql(ConnectionString)
                .UseSnakeCaseNamingConvention()
                .Options
        );

    /// <summary>
    /// A factory with this fixture's settings plus <paramref name="configure"/>.
    /// It is disposed with the fixture.
    /// </summary>
    public Task<WebApplicationFactory<Program>> CreateFactoryAsync(
        Action<IWebHostBuilder>? configure
    ) => Task.FromResult(Factory.WithWebHostBuilder(builder => configure?.Invoke(builder)));

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await SharedPostgres.DropDatabaseAsync(_databaseName);
    }
}
