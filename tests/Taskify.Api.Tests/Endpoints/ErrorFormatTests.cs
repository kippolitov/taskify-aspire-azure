using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Taskify.Api.Tests.Infrastructure;

namespace Taskify.Api.Tests.Endpoints;

/// <summary>
/// One error format: Problem Details everywhere, and no internals on a 500 (FR-012).
/// </summary>
public class ErrorFormatTests(TaskifyApiFixture fixture) : IClassFixture<TaskifyApiFixture>
{
    [Fact]
    public async Task NotFound_HasTheSameShape_ForEveryResource()
    {
        var client = await fixture.CreateClientAsAsync(1);
        string[] paths =
        [
            "/api/tasks/99999",
            "/api/projects/99999",
            "/api/users/99999",
            "/api/tasks/99999/comments",
        ];

        var shapes = new List<string>();
        foreach (var path in paths)
        {
            var problem = await HttpAssert.ProblemAsync(
                await client.GetAsync(path),
                HttpStatusCode.NotFound
            );
            var keys = problem.EnumerateObject().Select(p => p.Name).ToList();
            Assert.Contains("type", keys);
            Assert.Contains("title", keys);
            Assert.Contains("status", keys);
            Assert.Contains("detail", keys);
            shapes.Add(string.Join(",", keys.Order(StringComparer.Ordinal)));
        }

        Assert.Single(shapes.Distinct());
    }

    [Fact]
    public async Task UnexpectedFailure_Returns500ProblemDetails_WithoutInternals()
    {
        var factory = await fixture.CreateFactoryAsync(b =>
        {
            b.UseSetting(
                "ConnectionStrings:taskifydb",
                "Host=127.0.0.1;Port=1;Database=x;Username=x;Password=x;Timeout=1"
            );
            // Fail at once instead of retrying the unreachable server.
            b.UseSetting("Aspire:Npgsql:EntityFrameworkCore:PostgreSQL:DisableRetry", "true");
        });

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await fixture.GetTokenAsync(1)
        );

        var response = await client.GetAsync("/api/projects");

        var problem = await HttpAssert.ProblemAsync(response, HttpStatusCode.InternalServerError);
        var body = problem.GetRawText();
        Assert.False(problem.TryGetProperty("exception", out _));
        Assert.False(problem.TryGetProperty("stackTrace", out _));
        Assert.DoesNotContain("Npgsql", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("   at ", body, StringComparison.Ordinal);
    }
}
