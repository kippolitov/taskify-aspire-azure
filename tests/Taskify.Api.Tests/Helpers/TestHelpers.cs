using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Taskify.Api.Data;
using Taskify.Api.Data.Entities;
using Taskify.Shared.Enums;

namespace Taskify.Api.Tests.Helpers;

/// <summary>Creates a TaskifyDbContext backed by EF InMemory for unit tests.</summary>
public static class TestDbFactory
{
    public static TaskifyDbContext CreateWithSeed(string dbName)
    {
        var options = new DbContextOptionsBuilder<TaskifyDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var db = new TaskifyDbContext(options);

        var seed = new DateTimeOffset(2026, 3, 5, 12, 0, 0, TimeSpan.Zero);

        db.Users.AddRange(
            new User
            {
                Id = 1,
                DisplayName = "Alice Chen",
                Role = UserRole.ProductManager,
            },
            new User
            {
                Id = 2,
                DisplayName = "Bob Kim",
                Role = UserRole.Engineer,
            },
            new User
            {
                Id = 3,
                DisplayName = "Priya Sharma",
                Role = UserRole.Engineer,
            },
            new User
            {
                Id = 4,
                DisplayName = "David Lee",
                Role = UserRole.Engineer,
            },
            new User
            {
                Id = 5,
                DisplayName = "Sofia Reyes",
                Role = UserRole.Engineer,
            }
        );

        db.Projects.AddRange(
            new Project
            {
                Id = 1,
                Name = "Mobile Relaunch",
                Description = "Redesign the consumer mobile app",
                CreatedAt = seed,
            },
            new Project
            {
                Id = 2,
                Name = "API Gateway v2",
                Description = "Replace legacy gateway",
                CreatedAt = seed,
            }
        );

        db.TaskItems.AddRange(
            new TaskItem
            {
                Id = 1,
                ProjectId = 1,
                Title = "Set up CI pipeline",
                Status = ColumnStatus.ToDo,
                CreatedAt = seed,
                UpdatedAt = seed,
            },
            new TaskItem
            {
                Id = 2,
                ProjectId = 1,
                Title = "Implement login",
                Status = ColumnStatus.InProgress,
                AssigneeId = 2,
                CreatedAt = seed,
                UpdatedAt = seed,
            }
        );

        db.Comments.Add(
            new Comment
            {
                Id = 1,
                TaskItemId = 1,
                AuthorId = 1,
                Text = "Initial comment",
                CreatedAt = seed,
            }
        );

        db.SaveChanges();
        return db;
    }
}

/// <summary>
/// A no-op IHubContext implementation for unit tests (avoids needing Moq).
/// </summary>
public sealed class NullHubContext<THub> : IHubContext<THub>
    where THub : Hub
{
    public IHubClients Clients { get; } = new NullHubClients();

    public IGroupManager Groups { get; } = new NullGroupManager();
}

file sealed class NullHubClients : IHubClients
{
    private static readonly IClientProxy Noop = new NullClientProxy();

    public IClientProxy All => Noop;

    public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => Noop;

    public IClientProxy Client(string connectionId) => Noop;

    public IClientProxy Clients(IReadOnlyList<string> connectionIds) => Noop;

    public IClientProxy Group(string groupName) => Noop;

    public IClientProxy GroupExcept(
        string groupName,
        IReadOnlyList<string> excludedConnectionIds
    ) => Noop;

    public IClientProxy Groups(IReadOnlyList<string> groupNames) => Noop;

    public IClientProxy User(string userId) => Noop;

    public IClientProxy Users(IReadOnlyList<string> userIds) => Noop;
}

file sealed class NullClientProxy : IClientProxy
{
    public Task SendCoreAsync(
        string method,
        object?[] args,
        CancellationToken cancellationToken = default
    ) => Task.CompletedTask;
}

file sealed class NullGroupManager : IGroupManager
{
    public Task AddToGroupAsync(
        string connectionId,
        string groupName,
        CancellationToken cancellationToken = default
    ) => Task.CompletedTask;

    public Task RemoveFromGroupAsync(
        string connectionId,
        string groupName,
        CancellationToken cancellationToken = default
    ) => Task.CompletedTask;
}
