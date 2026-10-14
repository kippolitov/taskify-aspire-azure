using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Taskify.Shared;
using Taskify.Shared.Dtos;
using Taskify.Shared.Enums;
using Taskify.Shared.Requests;

namespace Taskify.Web.Services;

/// <summary>
/// Typed HTTP client for Taskify.Api — all REST calls go through here, and each carries the
/// Active User's identity. A typed client is created in the circuit's scope, so it sees that
/// circuit's <see cref="IdentityService"/>.
/// </summary>
public class ApiClient(HttpClient http, IdentityService identity)
{
    private static JsonSerializerOptions Json => TaskifyJson.Options;

    // ── Users ────────────────────────────────────────────────────────────
    public Task<List<UserDto>?> GetUsersAsync(CancellationToken ct = default) =>
        GetAsync<List<UserDto>>("api/users", ct);

    public Task<UserDto?> GetUserAsync(int id, CancellationToken ct = default) =>
        GetAsync<UserDto>($"api/users/{id}", ct);

    // ── Projects ─────────────────────────────────────────────────────────
    public Task<List<ProjectDto>?> GetProjectsAsync(CancellationToken ct = default) =>
        GetAsync<List<ProjectDto>>("api/projects", ct);

    public Task<ProjectDto?> GetProjectAsync(int id, CancellationToken ct = default) =>
        GetAsync<ProjectDto>($"api/projects/{id}", ct);

    // ── Tasks ────────────────────────────────────────────────────────────
    public Task<List<TaskDto>?> GetProjectTasksAsync(
        int projectId,
        CancellationToken ct = default
    ) => GetAsync<List<TaskDto>>($"api/projects/{projectId}/tasks", ct);

    public Task<TaskDto?> GetTaskAsync(int id, CancellationToken ct = default) =>
        GetAsync<TaskDto>($"api/tasks/{id}", ct);

    public Task<TaskDto?> CreateTaskAsync(
        int projectId,
        string title,
        string? description,
        int? assigneeId,
        CancellationToken ct = default
    ) =>
        SendAsync<TaskDto>(
            HttpMethod.Post,
            $"api/projects/{projectId}/tasks",
            new CreateTaskRequest(title, description, assigneeId),
            ct
        );

    /// <summary>Sets the title and description; leaves the Assignee and Status untouched.</summary>
    public Task<TaskDto?> UpdateTaskAsync(
        int id,
        string title,
        string? description,
        CancellationToken ct = default
    ) =>
        SendAsync<TaskDto>(
            HttpMethod.Put,
            $"api/tasks/{id}",
            new UpdateTaskRequest(title, description),
            ct
        );

    /// <summary>Sets or clears the Assignee; leaves everything else untouched.</summary>
    public Task<TaskDto?> AssignTaskAsync(
        int id,
        int? assigneeId,
        CancellationToken ct = default
    ) =>
        SendAsync<TaskDto>(
            HttpMethod.Patch,
            $"api/tasks/{id}/assignee",
            new AssignTaskRequest(assigneeId),
            ct
        );

    public Task<TaskDto?> MoveTaskAsync(
        int id,
        ColumnStatus status,
        CancellationToken ct = default
    ) =>
        SendAsync<TaskDto>(
            HttpMethod.Patch,
            $"api/tasks/{id}/status",
            new MoveTaskRequest(status),
            ct
        );

    // ── Comments ─────────────────────────────────────────────────────────
    public Task<List<CommentDto>?> GetTaskCommentsAsync(
        int taskId,
        CancellationToken ct = default
    ) => GetAsync<List<CommentDto>>($"api/tasks/{taskId}/comments", ct);

    /// <summary>The Author is the Active User, taken by the server from the identity.</summary>
    public Task<CommentDto?> AddCommentAsync(
        int taskId,
        string text,
        CancellationToken ct = default
    ) =>
        SendAsync<CommentDto>(
            HttpMethod.Post,
            $"api/tasks/{taskId}/comments",
            new AddCommentRequest(text),
            ct
        );

    public Task<CommentDto?> EditCommentAsync(
        int id,
        string text,
        CancellationToken ct = default
    ) =>
        SendAsync<CommentDto>(
            HttpMethod.Put,
            $"api/comments/{id}",
            new EditCommentRequest(text),
            ct
        );

    public async Task DeleteCommentAsync(int id, CancellationToken ct = default)
    {
        using var response = await SendAsync(HttpMethod.Delete, $"api/comments/{id}", null, ct);
        response.EnsureSuccessStatusCode();
    }

    private Task<T?> GetAsync<T>(string path, CancellationToken ct) =>
        SendAsync<T>(HttpMethod.Get, path, null, ct);

    private async Task<T?> SendAsync<T>(
        HttpMethod method,
        string path,
        object? body,
        CancellationToken ct
    )
    {
        using var response = await SendAsync(method, path, body, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(Json, ct);
    }

    /// <summary>The one place a request is sent, with the bearer token when there is one.</summary>
    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        object? body,
        CancellationToken ct
    )
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType(), options: Json);
        }

        if (await identity.GetAccessTokenAsync() is { } token)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await http.SendAsync(request, ct);
    }
}
