using System.Net;
using System.Text.Json;

namespace Taskify.Api.Tests.Endpoints;

public class NotificationEndpointsTests(Infrastructure.TaskifyApiFixture fixture)
    : IClassFixture<Infrastructure.TaskifyApiFixture>
{
    [Fact]
    public async Task GetAll_ReturnsEmptyArray()
    {
        var response = await (await fixture.CreateClientAsAsync(1)).GetAsync("/api/notifications");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Array, body.RootElement.ValueKind);
        Assert.Equal(0, body.RootElement.GetArrayLength());
    }
}
