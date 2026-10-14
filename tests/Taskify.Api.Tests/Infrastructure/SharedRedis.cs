using Testcontainers.Redis;

namespace Taskify.Api.Tests.Infrastructure;

/// <summary>
/// One Redis 7 container per test process, for the SignalR backplane tests. The
/// Testcontainers resource reaper removes it when the process exits.
/// </summary>
public static class SharedRedis
{
    private static readonly Lazy<Task<RedisContainer>> Container = new(StartAsync);

    public static async Task<string> GetConnectionStringAsync() =>
        (await Container.Value).GetConnectionString();

    private static async Task<RedisContainer> StartAsync()
    {
        var container = new RedisBuilder("redis:7").Build();
        await container.StartAsync();
        return container;
    }
}
