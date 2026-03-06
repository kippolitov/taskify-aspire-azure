using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Taskify.Api.Data;
using Taskify.Api.Data.Entities;
using Taskify.Api.Hubs;
using Taskify.Api.Services;
using Taskify.Shared.Enums;

namespace Taskify.Benchmarks;

/// <summary>
/// T043 — BenchmarkDotNet baselines for GET tasks and PATCH status.
/// Uses in-memory database to isolate from I/O so results reflect pure
/// service + EF Core overhead.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
public class ApiBenchmarks
{
    private TaskifyDbContext _db = null!;
    private TaskService _service = null!;
    private int _toggleCounter;

    [GlobalSetup]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<TaskifyDbContext>()
            .UseInMemoryDatabase("bench-db")
            .Options;

        _db = new TaskifyDbContext(options);

        var seed = DateTimeOffset.UtcNow;

        _db.Users.Add(
            new User
            {
                Id = 1,
                DisplayName = "Alice Chen",
                Role = UserRole.ProductManager,
            }
        );
        _db.Projects.Add(
            new Project
            {
                Id = 1,
                Name = "Mobile Relaunch",
                Description = "desc",
                CreatedAt = seed,
            }
        );

        for (var i = 1; i <= 20; i++)
        {
            _db.TaskItems.Add(
                new TaskItem
                {
                    Id = i,
                    ProjectId = 1,
                    Title = $"Task {i}",
                    Status = (ColumnStatus)(i % 4),
                    CreatedAt = seed,
                    UpdatedAt = seed,
                }
            );
        }

        _db.SaveChanges();

        _service = new TaskService(_db, new BenchmarkNullHubContext());
    }

    [GlobalCleanup]
    public void Cleanup() => _db.Dispose();

    /// <summary>Benchmark: retrieve all tasks for project 1 (20 tasks).</summary>
    [Benchmark]
    public async Task GetProjectTasks() => await _service.GetProjectTasksAsync(1);

    /// <summary>Benchmark: move a task between two statuses (alternating).</summary>
    [Benchmark]
    public async Task MoveTaskStatus()
    {
        var newStatus = (_toggleCounter++ % 2 == 0) ? ColumnStatus.InProgress : ColumnStatus.ToDo;

        await _service.MoveTaskAsync(1, newStatus);
    }
}

// ── Minimal no-op IHubContext for benchmarks ──────────────────────────────────

file sealed class BenchmarkNullHubContext : IHubContext<TaskifyHub>
{
    public IHubClients Clients { get; } = new NullClients();
    public IGroupManager Groups { get; } = new NullGroups();

    private sealed class NullClients : IHubClients
    {
        private static readonly IClientProxy Noop = new NullProxy();
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

    private sealed class NullProxy : IClientProxy
    {
        public Task SendCoreAsync(
            string method,
            object?[] args,
            CancellationToken cancellationToken = default
        ) => Task.CompletedTask;
    }

    private sealed class NullGroups : IGroupManager
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
}
