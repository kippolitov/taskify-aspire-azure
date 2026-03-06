using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Taskify.Api.Controllers;
using Taskify.Api.Data;
using Taskify.Api.Tests.Helpers;
using Taskify.Shared.Dtos;
using Taskify.Shared.Enums;

namespace Taskify.Api.Tests.Controllers;

/// <summary>T041 — Integration-style unit tests for UsersController, ProjectsController, NotificationsController.</summary>
public class UsersControllerTests
{
    private static UsersController Build(string name) => new(TestDbFactory.CreateWithSeed(name));

    [Fact]
    public async Task GetAll_ReturnsAllFiveUsers()
    {
        var ctrl = Build(nameof(GetAll_ReturnsAllFiveUsers));

        var result = await ctrl.GetAll();

        var ok = Assert.IsType<OkObjectResult>(result);
        var users = Assert.IsAssignableFrom<IEnumerable<UserDto>>(ok.Value);
        Assert.Equal(5, users.Count());
    }

    [Fact]
    public async Task GetAll_UsersAreOrderedById()
    {
        var ctrl = Build(nameof(GetAll_UsersAreOrderedById));

        var result = await ctrl.GetAll();

        var ok = Assert.IsType<OkObjectResult>(result);
        var users = Assert.IsAssignableFrom<IEnumerable<UserDto>>(ok.Value).ToList();
        Assert.Equal([1, 2, 3, 4, 5], users.Select(u => u.Id));
    }

    [Fact]
    public async Task GetById_ExistingUser_ReturnsOkWithCorrectRole()
    {
        var ctrl = Build(nameof(GetById_ExistingUser_ReturnsOkWithCorrectRole));

        var result = await ctrl.GetById(1);

        var ok = Assert.IsType<OkObjectResult>(result);
        var user = Assert.IsType<UserDto>(ok.Value);
        Assert.Equal("Alice Chen", user.DisplayName);
        Assert.Equal(UserRole.ProductManager, user.Role);
    }

    [Fact]
    public async Task GetById_NonExistingUser_Returns404()
    {
        var ctrl = Build(nameof(GetById_NonExistingUser_Returns404));

        var result = await ctrl.GetById(999);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, problem.StatusCode);
    }
}

public class ProjectsControllerTests
{
    private static ProjectsController Build(string name) => new(TestDbFactory.CreateWithSeed(name));

    [Fact]
    public async Task GetAll_ReturnsSeedProjects()
    {
        var ctrl = Build(nameof(GetAll_ReturnsSeedProjects));

        var result = await ctrl.GetAll();

        var ok = Assert.IsType<OkObjectResult>(result);
        var projects = Assert.IsAssignableFrom<IEnumerable<ProjectDto>>(ok.Value);
        Assert.Equal(2, projects.Count());
    }

    [Fact]
    public async Task GetAll_ProjectsAreOrderedById()
    {
        var ctrl = Build(nameof(GetAll_ProjectsAreOrderedById));

        var result = await ctrl.GetAll();

        var ok = Assert.IsType<OkObjectResult>(result);
        var projects = Assert.IsAssignableFrom<IEnumerable<ProjectDto>>(ok.Value).ToList();
        Assert.Equal([1, 2], projects.Select(p => p.Id));
    }

    [Fact]
    public async Task GetById_ExistingProject_ReturnsCorrectProject()
    {
        var ctrl = Build(nameof(GetById_ExistingProject_ReturnsCorrectProject));

        var result = await ctrl.GetById(1);

        var ok = Assert.IsType<OkObjectResult>(result);
        var project = Assert.IsType<ProjectDto>(ok.Value);
        Assert.Equal("Mobile Relaunch", project.Name);
    }

    [Fact]
    public async Task GetById_NonExistingProject_Returns404()
    {
        var ctrl = Build(nameof(GetById_NonExistingProject_Returns404));

        var result = await ctrl.GetById(999);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, problem.StatusCode);
    }
}

public class NotificationsControllerTests
{
    [Fact]
    public void GetAll_ReturnsOkWithEmptyArray()
    {
        var ctrl = new NotificationsController();

        var result = ctrl.GetAll();

        var ok = Assert.IsType<OkObjectResult>(result);
        var items = Assert.IsAssignableFrom<IEnumerable<object>>(ok.Value);
        Assert.Empty(items);
    }
}
