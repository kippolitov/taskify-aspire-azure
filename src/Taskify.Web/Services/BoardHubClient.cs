using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.ServiceDiscovery;
using Taskify.Shared;
using Taskify.Shared.Dtos;
using Taskify.Shared.Realtime;

namespace Taskify.Web.Services;

/// <summary>
/// Manages the SignalR connection to TaskifyHub on the API. It resolves the API's address
/// through service discovery, then connects with WebSockets only and no negotiation, so any
/// API instance can serve it without sticky sessions (research R15).
/// </summary>
public sealed class BoardHubClient : IAsyncDisposable
{
    // https+http:// is the Aspire service-discovery scheme — not a literal hardcoded URL
#pragma warning disable S1075
    private const string ApiServiceName = "https+http://taskify-api";
#pragma warning restore S1075

    private HubConnection? _connection;
    private readonly ServiceEndpointResolver _resolver;
    private readonly IdentityService _identity;
    private int? _currentProjectId;

    public event Func<TaskMovedMessage, Task>? OnTaskMoved;
    public event Func<TaskAssignedMessage, Task>? OnTaskAssigned;
    public event Func<TaskDto, Task>? OnTaskCreated;
    public event Func<CommentAddedMessage, Task>? OnCommentAdded;
    public event Func<CommentEditedMessage, Task>? OnCommentEdited;
    public event Func<CommentDeletedMessage, Task>? OnCommentDeleted;

    public BoardHubClient(ServiceEndpointResolver resolver, IdentityService identity)
    {
        _resolver = resolver;
        _identity = identity;
    }

    public async Task StartAsync(int projectId, CancellationToken ct = default)
    {
        if (_connection is not null)
        {
            if (_currentProjectId == projectId)
            {
                return; // already on this board
            }

            await StopAsync();
        }

        var hubUrl = await ResolveHubUrlAsync(ct);
        _currentProjectId = projectId;

        _connection = new HubConnectionBuilder()
            .WithUrl(
                hubUrl,
                o =>
                {
                    o.Transports = HttpTransportType.WebSockets;
                    o.SkipNegotiation = true;
                    // Called before every connect and reconnect, so an old token is renewed first.
                    o.AccessTokenProvider = _identity.GetAccessTokenAsync;
                }
            )
            .AddJsonProtocol(o => TaskifyJson.Configure(o.PayloadSerializerOptions))
            .WithAutomaticReconnect()
            .Build();

        RegisterHandlers(_connection);

        _connection.Reconnected += async _ =>
        {
            if (_currentProjectId.HasValue)
            {
                await _connection.SendAsync("JoinBoard", _currentProjectId.Value);
            }
        };

        await _connection.StartAsync(ct);
        await _connection.SendAsync("JoinBoard", projectId, ct);
    }

    /// <summary>
    /// WebSockets bypass HttpClient, so the <c>https+http://</c> name must be resolved to a
    /// real address first.
    /// </summary>
    private async Task<Uri> ResolveHubUrlAsync(CancellationToken ct)
    {
        var source = await _resolver.GetEndpointsAsync(ApiServiceName, ct);
        var endpoint =
            source.Endpoints.Select(e => e.EndPoint).OfType<UriEndPoint>().FirstOrDefault()
            ?? throw new InvalidOperationException(
                "Service discovery returned no address for taskify-api."
            );

        return new Uri(endpoint.Uri, "hubs/taskify");
    }

    public async Task StopAsync()
    {
        if (_connection is null)
        {
            return;
        }

        if (_currentProjectId.HasValue)
        {
            try
            {
                await _connection.SendAsync("LeaveBoard", _currentProjectId.Value);
            }
            catch
            {
                // ignore on shutdown
            }
        }

        await _connection.StopAsync();
        await _connection.DisposeAsync();
        _connection = null;
        _currentProjectId = null;
    }

    private void RegisterHandlers(HubConnection conn)
    {
        RegisterTaskHandlers(conn);
        RegisterCommentHandlers(conn);
    }

    private void RegisterTaskHandlers(HubConnection conn)
    {
        conn.On<TaskMovedMessage>(
            nameof(ITaskifyClient.TaskMoved),
            m => OnTaskMoved?.Invoke(m) ?? Task.CompletedTask
        );
        conn.On<TaskAssignedMessage>(
            nameof(ITaskifyClient.TaskAssigned),
            m => OnTaskAssigned?.Invoke(m) ?? Task.CompletedTask
        );
        conn.On<TaskDto>(
            nameof(ITaskifyClient.TaskCreated),
            t => OnTaskCreated?.Invoke(t) ?? Task.CompletedTask
        );
    }

    private void RegisterCommentHandlers(HubConnection conn)
    {
        conn.On<CommentAddedMessage>(
            nameof(ITaskifyClient.CommentAdded),
            m => OnCommentAdded?.Invoke(m) ?? Task.CompletedTask
        );
        conn.On<CommentEditedMessage>(
            nameof(ITaskifyClient.CommentEdited),
            m => OnCommentEdited?.Invoke(m) ?? Task.CompletedTask
        );
        conn.On<CommentDeletedMessage>(
            nameof(ITaskifyClient.CommentDeleted),
            m => OnCommentDeleted?.Invoke(m) ?? Task.CompletedTask
        );
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}
