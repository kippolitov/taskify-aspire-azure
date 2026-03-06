using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Taskify.Shared.Dtos;
using Taskify.Shared.Enums;

namespace Taskify.Web.Services;

/// <summary>
/// Typed HTTP client for Taskify.Api — all REST calls go through here.
/// </summary>
public class ApiClient(HttpClient http)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    // ── Users ────────────────────────────────────────────────────────────
    public Task<List<UserDto>?> GetUsersAsync() =>
        http.GetFromJsonAsync<List<UserDto>>("api/users", JsonOpts);

    public Task<UserDto?> GetUserAsync(int id) =>
        http.GetFromJsonAsync<UserDto>($"api/users/{id}", JsonOpts);

    // ── Projects ─────────────────────────────────────────────────────────
    public Task<List<ProjectDto>?> GetProjectsAsync() =>
        http.GetFromJsonAsync<List<ProjectDto>>("api/projects", JsonOpts);

    public Task<ProjectDto?> GetProjectAsync(int id) =>
        http.GetFromJsonAsync<ProjectDto>($"api/projects/{id}", JsonOpts);

    // ── Tasks ────────────────────────────────────────────────────────────
    public Task<List<TaskDto>?> GetProjectTasksAsync(int projectId) =>
        http.GetFromJsonAsync<List<TaskDto>>($"api/projects/{projectId}/tasks", JsonOpts);

    public Task<TaskDto?> GetTaskAsync(int id) =>
        http.GetFromJsonAsync<TaskDto>($"api/tasks/{id}", JsonOpts);

    public async Task<TaskDto?> CreateTaskAsync(
        int projectId,
        string title,
        string? description,
        int? assigneeId
    )
    {
        var resp = await http.PostAsJsonAsync(
            $"api/projects/{projectId}/tasks",
            new
            {
                title,
                description,
                assigneeId,
            },
            JsonOpts
        );
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<TaskDto>(JsonOpts);
    }

    public async Task<TaskDto?> UpdateTaskAsync(
        int id,
        string? title,
        string? description,
        int? assigneeId
    )
    {
        var resp = await http.PutAsJsonAsync(
            $"api/tasks/{id}",
            new
            {
                title,
                description,
                assigneeId,
            },
            JsonOpts
        );
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<TaskDto>(JsonOpts);
    }

    public async Task<TaskDto?> MoveTaskAsync(int id, ColumnStatus status)
    {
        var resp = await http.PatchAsJsonAsync($"api/tasks/{id}/status", new { status }, JsonOpts);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<TaskDto>(JsonOpts);
    }

    /// <summary>Updates only the assignee; leaves title and description untouched (T032).</summary>
    public async Task<TaskDto?> UpdateTaskAssigneeAsync(int id, int? assigneeId)
    {
        var resp = await http.PutAsJsonAsync($"api/tasks/{id}", new { assigneeId }, JsonOpts);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<TaskDto>(JsonOpts);
    }

    // ── Comments ─────────────────────────────────────────────────────────
    public Task<List<CommentDto>?> GetTaskCommentsAsync(int taskId) =>
        http.GetFromJsonAsync<List<CommentDto>>($"api/tasks/{taskId}/comments", JsonOpts);

    public async Task<CommentDto?> AddCommentAsync(int taskId, int authorId, string text)
    {
        var resp = await http.PostAsJsonAsync(
            $"api/tasks/{taskId}/comments",
            new { authorId, text },
            JsonOpts
        );
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<CommentDto>(JsonOpts);
    }

    public async Task<CommentDto?> EditCommentAsync(int id, int requestingUserId, string text)
    {
        var resp = await http.PutAsJsonAsync(
            $"api/comments/{id}",
            new { requestingUserId, text },
            JsonOpts
        );
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<CommentDto>(JsonOpts);
    }

    public async Task DeleteCommentAsync(int id, int requestingUserId)
    {
        var resp = await http.DeleteAsync($"api/comments/{id}?requestingUserId={requestingUserId}");
        resp.EnsureSuccessStatusCode();
    }
}
