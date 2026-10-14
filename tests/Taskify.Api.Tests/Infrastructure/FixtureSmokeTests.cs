using System.Net;
using System.Net.Http.Json;
using Taskify.Shared.Dtos;

namespace Taskify.Api.Tests.Infrastructure;

public class FixtureSmokeTests(TaskifyApiFixture fixture) : IClassFixture<TaskifyApiFixture>
{
    [Fact]
    public async Task GetUsers_ReturnsFiveSeededUsers_FromPostgres()
    {
        var client = fixture.CreateClient();

        var response = await client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var users = await response.Content.ReadFromJsonAsync<List<UserDto>>(TestJson.Options);
        Assert.NotNull(users);
        Assert.Equal(5, users.Count);
        Assert.Equal("Jordan Rivera", users[0].DisplayName);
    }
}
