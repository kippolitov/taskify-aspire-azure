using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Http;
using Taskify.Shared.Dtos;
using Taskify.Shared.Enums;

namespace Taskify.Web.Tests.Helpers;

/// <summary>
/// A simple delegating handler that returns pre-configured JSON responses by URL path.
/// Use in bUnit tests to avoid real HTTP calls from ApiClient.
/// </summary>
public sealed class TestApiHandler : HttpMessageHandler
{
    private readonly Dictionary<string, string> _responses;

    public static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public TestApiHandler(Dictionary<string, string> responses)
    {
        _responses = responses;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        var path = request.RequestUri?.AbsolutePath ?? "";

        foreach (var (pattern, json) in _responses)
        {
            if (path.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json"),
                };
                return Task.FromResult(response);
            }
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    /// <summary>Builds a json string from an object using the standard Taskify serializer options.</summary>
    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOpts);
}

/// <summary>
/// A no-op IHttpMessageHandlerFactory for use when registering BoardHubClient in bUnit tests.
/// Returns a handler that immediately returns 503 Service Unavailable so Hub.StartAsync fails fast.
/// </summary>
public sealed class NullHttpMessageHandlerFactory : IHttpMessageHandlerFactory
{
    public HttpMessageHandler CreateHandler(string name) => new NullHttpHandler();

    private sealed class NullHttpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable));
    }
}

/// <summary>
/// Seed data constants used across multiple bUnit tests.
/// </summary>
public static class TestData
{
    public static readonly List<UserDto> FiveUsers =
    [
        new(1, "Alice Chen", UserRole.ProductManager),
        new(2, "Bob Kim", UserRole.Engineer),
        new(3, "Priya Sharma", UserRole.Engineer),
        new(4, "David Lee", UserRole.Engineer),
        new(5, "Sofia Reyes", UserRole.Engineer),
    ];

    public static readonly List<ProjectDto> ThreeProjects =
    [
        new(
            1,
            "Mobile Relaunch",
            "Redesign the consumer mobile app",
            new DateTimeOffset(2026, 3, 5, 12, 0, 0, TimeSpan.Zero)
        ),
        new(
            2,
            "API Gateway v2",
            "Replace legacy gateway",
            new DateTimeOffset(2026, 3, 5, 12, 0, 0, TimeSpan.Zero)
        ),
        new(
            3,
            "Design System",
            "Shared component library",
            new DateTimeOffset(2026, 3, 5, 12, 0, 0, TimeSpan.Zero)
        ),
    ];

    public static readonly List<TaskDto> SampleTasks =
    [
        new(
            1,
            1,
            "Set up CI pipeline",
            null,
            ColumnStatus.ToDo,
            null,
            new DateTimeOffset(2026, 3, 5, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 3, 5, 12, 0, 0, TimeSpan.Zero),
            0
        ),
        new(
            2,
            1,
            "Implement login",
            "OAuth flow",
            ColumnStatus.InProgress,
            FiveUsers[1],
            new DateTimeOffset(2026, 3, 5, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 3, 5, 12, 0, 0, TimeSpan.Zero),
            2
        ),
    ];
}
