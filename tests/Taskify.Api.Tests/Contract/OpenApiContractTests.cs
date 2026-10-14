using System.Text.Json;
using Taskify.Api.Tests.Infrastructure;

namespace Taskify.Api.Tests.Contract;

/// <summary>
/// The OpenAPI document describes every operation's success and error shapes (FR-013, SC-003).
/// </summary>
[Trait("Category", "Contract")]
public class OpenApiContractTests(TaskifyApiFixture fixture) : IClassFixture<TaskifyApiFixture>
{
    private static readonly string[] HttpMethods = ["get", "put", "post", "delete", "patch"];

    private async Task<JsonElement> GetDocumentAsync()
    {
        var json = await fixture.CreateClient().GetStringAsync("/openapi/v1.json");
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static IEnumerable<(string Path, string Method, JsonElement Operation)> Operations(
        JsonElement document
    ) =>
        document
            .GetProperty("paths")
            .EnumerateObject()
            .SelectMany(path =>
                path.Value.EnumerateObject()
                    .Where(op => HttpMethods.Contains(op.Name))
                    .Select(op => (path.Name, op.Name, op.Value))
            );

    [Fact]
    public async Task Document_IsOpenApi31()
    {
        var document = await GetDocumentAsync();

        Assert.StartsWith("3.1", document.GetProperty("openapi").GetString());
    }

    [Fact]
    public async Task EveryOperation_DescribesASuccessAndAnErrorResponse()
    {
        var document = await GetDocumentAsync();
        var operations = Operations(document).ToList();

        Assert.NotEmpty(operations);
        Assert.All(
            operations,
            op =>
            {
                var codes = op
                    .Operation.GetProperty("responses")
                    .EnumerateObject()
                    .Select(r => r.Name)
                    .ToList();
                Assert.True(
                    codes.Exists(c => c.StartsWith('2')),
                    $"{op.Method.ToUpperInvariant()} {op.Path} has no 2xx response."
                );
                Assert.True(
                    codes.Exists(c => c.StartsWith('4') || c.StartsWith('5')),
                    $"{op.Method.ToUpperInvariant()} {op.Path} has no 4xx or 5xx response."
                );
            }
        );
    }

    [Fact]
    public async Task TaskUpdate_AndAssigneeChange_AreSeparateOperations()
    {
        var paths = (await GetDocumentAsync()).GetProperty("paths");

        Assert.True(paths.GetProperty("/api/tasks/{id}").TryGetProperty("put", out _));
        Assert.True(paths.GetProperty("/api/tasks/{id}/assignee").TryGetProperty("patch", out _));
    }
}
