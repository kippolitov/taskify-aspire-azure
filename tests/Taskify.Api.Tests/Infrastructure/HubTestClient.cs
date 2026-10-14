using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Taskify.Shared;

namespace Taskify.Api.Tests.Infrastructure;

/// <summary>
/// A real SignalR subscriber connected to the in-memory API over long polling.
/// </summary>
public sealed class HubTestClient : IAsyncDisposable
{
    private readonly HubConnection _connection;

    private HubTestClient(HubConnection connection) => _connection = connection;

    public static async Task<HubTestClient> ConnectAsync(TestServer server, string? accessToken)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(
                new Uri(server.BaseAddress, "hubs/taskify"),
                o =>
                {
                    o.HttpMessageHandlerFactory = _ => server.CreateHandler();
                    o.Transports = HttpTransportType.LongPolling;
                    o.AccessTokenProvider = () => Task.FromResult(accessToken);
                }
            )
            .AddJsonProtocol(o => TaskifyJson.Configure(o.PayloadSerializerOptions))
            .Build();

        await connection.StartAsync();
        return new HubTestClient(connection);
    }

    public Task JoinBoardAsync(int projectId) => _connection.InvokeAsync("JoinBoard", projectId);

    public Task LeaveBoardAsync(int projectId) => _connection.InvokeAsync("LeaveBoard", projectId);

    /// <summary>
    /// Starts listening for <paramref name="methodName"/> at once, and completes with the
    /// first message. Throws <see cref="TimeoutException"/> when none arrives in time.
    /// Call it before triggering the change.
    /// </summary>
    public async Task<T> WaitForAsync<T>(string methodName, TimeSpan timeout)
    {
        var received = new TaskCompletionSource<T>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        using var subscription = _connection.On<T>(methodName, m => received.TrySetResult(m));

        return await received.Task.WaitAsync(timeout);
    }

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
}
