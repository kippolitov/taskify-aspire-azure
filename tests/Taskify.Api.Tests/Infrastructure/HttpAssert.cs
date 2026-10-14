using System.Net;
using System.Text.Json;

namespace Taskify.Api.Tests.Infrastructure;

public static class HttpAssert
{
    public const string ProblemJson = "application/problem+json";

    /// <summary>Asserts the status and the Problem Details content type, and returns the body.</summary>
    public static async Task<JsonElement> ProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode status
    )
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.Clone();
    }
}
