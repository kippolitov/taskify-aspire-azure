using Taskify.Shared.Enums;
using Taskify.Web.Services;
using Taskify.Web.Tests.Helpers;

namespace Taskify.Web.Tests.Services;

/// <summary>Every API call carries the Active User's identity.</summary>
public class ApiClientAuthTests
{
    [Fact]
    public async Task EveryCall_CarriesTheBearerToken()
    {
        var identity = await TestIdentity.SignedInAsync(TestData.FiveUsers[0]);
        var handler = new TestApiHandler([]);
        var api = new ApiClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") },
            identity
        );
        Func<Task>[] calls =
        [
            () => api.GetUsersAsync(),
            () => api.GetUserAsync(1),
            () => api.GetProjectsAsync(),
            () => api.GetProjectAsync(1),
            () => api.GetProjectTasksAsync(1),
            () => api.GetTaskAsync(1),
            () => api.CreateTaskAsync(1, "Title", null, null),
            () => api.UpdateTaskAsync(1, "Title", null),
            () => api.AssignTaskAsync(1, 2),
            () => api.MoveTaskAsync(1, ColumnStatus.Done),
            () => api.GetTaskCommentsAsync(1),
            () => api.AddCommentAsync(1, "Text"),
            () => api.EditCommentAsync(1, "Text"),
            () => api.DeleteCommentAsync(1),
        ];

        foreach (var call in calls)
        {
            // The handler answers 404 to everything; only the request matters here.
            await Assert.ThrowsAnyAsync<Exception>(call);
        }

        Assert.Equal(calls.Length, handler.Requests.Count);
        Assert.All(handler.Requests, r => Assert.Equal("Bearer token-1", r.Authorization));
    }

    [Fact]
    public async Task WithoutIdentity_SendsNoAuthorizationHeader()
    {
        var handler = new TestApiHandler(
            new Dictionary<string, string>
            {
                ["api/users"] = TestApiHandler.Serialize(TestData.FiveUsers),
            }
        );
        var api = new ApiClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") },
            TestIdentity.Create()
        );

        var users = await api.GetUsersAsync();

        Assert.Equal(5, users!.Count);
        Assert.Null(Assert.Single(handler.Requests).Authorization);
    }
}
