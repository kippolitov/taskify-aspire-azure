using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Http;
using Taskify.Shared.Dtos;
using Taskify.Shared.Enums;

namespace Taskify.Web.Services;

/// <summary>
/// Manages the SignalR connection to TaskifyHub on the API.
/// Uses IHttpMessageHandlerFactory so service-discovery resolves the URL.
/// </summary>
public sealed class BoardHubClient : IAsyncDisposable
{
    private HubConnection? _connection;
    private readonly IHttpMessageHandlerFactory _handlerFactory;
    private int? _currentProjectId;

    public event Func<int, string, string, DateTimeOffset, Task>? OnTaskMoved;
    public event Func<int, UserDto?, Task>? OnTaskAssigned;
    public event Func<TaskDto, Task>? OnTaskCreated;
    public event Func<int, CommentDto, Task>? OnCommentAdded;
    public event Func<int, int, string, DateTimeOffset?, Task>? OnCommentEdited;
    public event Func<int, int, Task>? OnCommentDeleted;

    public BoardHubClient(IHttpMessageHandlerFactory handlerFactory)
    {
        _handlerFactory = handlerFactory;
    }

    public async Task StartAsync(int projectId)
    {
        if (_connection is not null)
        {
            if (_currentProjectId == projectId)
            {
                return; // already on this board
            }

            await StopAsync();
        }

        _currentProjectId = projectId;

        _connection = new HubConnectionBuilder()
            .WithUrl(
#pragma warning disable S1075 // https+http:// is the Aspire service-discovery scheme
                "https+http://taskify-api/hubs/taskify",
#pragma warning restore S1075
                o =>
                    o.HttpMessageHandlerFactory = _ =>
                        _handlerFactory.CreateHandler("taskify-api-hub")
            )
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

        await _connection.StartAsync();
        await _connection.SendAsync("JoinBoard", projectId);
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
        conn.On<TaskMovedPayload>(
            "TaskMoved",
            p =>
                OnTaskMoved?.Invoke(p.TaskId, p.FromStatus, p.ToStatus, p.MovedAt)
                ?? Task.CompletedTask
        );

        conn.On<TaskAssignedPayload>(
            "TaskAssigned",
            p => OnTaskAssigned?.Invoke(p.TaskId, p.Assignee) ?? Task.CompletedTask
        );

        conn.On<TaskDto>("TaskCreated", t => OnTaskCreated?.Invoke(t) ?? Task.CompletedTask);

        conn.On<CommentAddedPayload>(
            "CommentAdded",
            p => OnCommentAdded?.Invoke(p.TaskId, p.Comment) ?? Task.CompletedTask
        );

        conn.On<CommentEditedPayload>(
            "CommentEdited",
            p =>
                OnCommentEdited?.Invoke(p.TaskId, p.CommentId, p.NewText, p.EditedAt)
                ?? Task.CompletedTask
        );

        conn.On<CommentDeletedPayload>(
            "CommentDeleted",
            p => OnCommentDeleted?.Invoke(p.TaskId, p.CommentId) ?? Task.CompletedTask
        );
    }

    public async ValueTask DisposeAsync() => await StopAsync();

    // ── Payload records ──────────────────────────────────────────────────

    private sealed record TaskMovedPayload(
        int TaskId,
        int ProjectId,
        string FromStatus,
        string ToStatus,
        DateTimeOffset MovedAt
    );

    private sealed record TaskAssignedPayload(int TaskId, int ProjectId, UserDto? Assignee);

    private sealed record CommentAddedPayload(int TaskId, CommentDto Comment);

    private sealed record CommentEditedPayload(
        int TaskId,
        int CommentId,
        string NewText,
        DateTimeOffset? EditedAt
    );

    private sealed record CommentDeletedPayload(int TaskId, int CommentId);
}
