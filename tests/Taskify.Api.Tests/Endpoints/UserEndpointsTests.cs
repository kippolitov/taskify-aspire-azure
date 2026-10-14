using System.Net;
using System.Net.Http.Json;
using Taskify.Api.Tests.Infrastructure;
using Taskify.Shared.Dtos;
using Taskify.Shared.Enums;

namespace Taskify.Api.Tests.Endpoints;

public class UserEndpointsTests(TaskifyApiFixture fixture)
    : IClassFixture<TaskifyApiFixture>,
        IAsyncLifetime
{
    private HttpClient _client = null!;

    public async Task InitializeAsync() => _client = await fixture.CreateClientAsAsync(1);

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetAll_ReturnsAllFiveUsers_OrderedById()
    {
        var users = await _client.GetFromJsonAsync<List<UserDto>>("/api/users", TestJson.Options);

        Assert.NotNull(users);
        Assert.Equal([1, 2, 3, 4, 5], users.Select(u => u.Id));
    }

    [Fact]
    public async Task GetById_ExistingUser_ReturnsUserWithRole()
    {
        var user = await _client.GetFromJsonAsync<UserDto>("/api/users/1", TestJson.Options);

        Assert.NotNull(user);
        Assert.Equal("Jordan Rivera", user.DisplayName);
        Assert.Equal(UserRole.ProductManager, user.Role);
    }

    [Fact]
    public async Task GetById_NonExistingUser_Returns404ProblemDetails()
    {
        var response = await _client.GetAsync("/api/users/999");

        await HttpAssert.ProblemAsync(response, HttpStatusCode.NotFound);
    }
}
