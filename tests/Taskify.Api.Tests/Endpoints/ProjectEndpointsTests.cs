using System.Net;
using System.Net.Http.Json;
using Taskify.Api.Tests.Infrastructure;
using Taskify.Shared.Dtos;

namespace Taskify.Api.Tests.Endpoints;

public class ProjectEndpointsTests(TaskifyApiFixture fixture)
    : IClassFixture<TaskifyApiFixture>,
        IAsyncLifetime
{
    private HttpClient _client = null!;

    public async Task InitializeAsync() => _client = await fixture.CreateClientAsAsync(1);

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetAll_ReturnsSeededProjects_OrderedById()
    {
        var projects = await _client.GetFromJsonAsync<List<ProjectDto>>(
            "/api/projects",
            TestJson.Options
        );

        Assert.NotNull(projects);
        Assert.Equal([1, 2, 3], projects.Select(p => p.Id));
    }

    [Fact]
    public async Task GetById_ExistingProject_ReturnsProject()
    {
        var project = await _client.GetFromJsonAsync<ProjectDto>(
            "/api/projects/1",
            TestJson.Options
        );

        Assert.NotNull(project);
        Assert.Equal("Mobile Relaunch", project.Name);
    }

    [Fact]
    public async Task GetById_NonExistingProject_Returns404ProblemDetails()
    {
        var response = await _client.GetAsync("/api/projects/999");

        await HttpAssert.ProblemAsync(response, HttpStatusCode.NotFound);
    }
}
