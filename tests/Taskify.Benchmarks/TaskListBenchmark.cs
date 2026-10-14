using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using Microsoft.EntityFrameworkCore;
using Taskify.Api.Data;
using Taskify.Api.Data.Entities;
using Taskify.Api.Services;
using Taskify.Shared.Enums;
using Testcontainers.PostgreSql;

namespace Taskify.Benchmarks;

/// <summary>
/// SC-004: the task list for one Project with 500 Tasks × 20 Comments, against PostgreSQL 16.
/// Budget: p95 ≤ 200 ms.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(P95Config))]
public class TaskListBenchmark
{
    private const int TaskCount = 500;
    private const int CommentsPerTask = 20;

    private PostgreSqlContainer _postgres = null!;
    private TaskifyDbContext _db = null!;
    private TaskService _service = null!;
    private int _projectId;

    [GlobalSetup]
    public async Task Setup()
    {
        _postgres = new PostgreSqlBuilder("postgres:16").Build();
        await _postgres.StartAsync();

        _db = new TaskifyDbContext(
            new DbContextOptionsBuilder<TaskifyDbContext>()
                .UseNpgsql(_postgres.GetConnectionString())
                .UseSnakeCaseNamingConvention()
                .Options
        );
        await _db.Database.MigrateAsync();

        var now = DateTimeOffset.UtcNow;
        var project = new Project { Name = "Benchmark project", CreatedAt = now };
        for (var i = 0; i < TaskCount; i++)
        {
            var task = new TaskItem
            {
                Title = $"Task {i}",
                Status = (ColumnStatus)(i % 4),
                AssigneeId = i % 5 + 1,
                CreatedAt = now,
                UpdatedAt = now,
            };
            for (var c = 0; c < CommentsPerTask; c++)
            {
                task.Comments.Add(
                    new Comment
                    {
                        AuthorId = c % 5 + 1,
                        Text = $"Comment {c} on task {i}",
                        CreatedAt = now,
                    }
                );
            }

            project.Tasks.Add(task);
        }

        _db.Projects.Add(project);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        _projectId = project.Id;
        _service = BenchmarkServices.CreateTaskService(_db);
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _db.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Benchmark]
    public async Task GetProjectTasks() =>
        await _service.GetProjectTasksAsync(_projectId, CancellationToken.None);
}

/// <summary>Adds the p95 column that SC-004's budget is checked against.</summary>
public sealed class P95Config : ManualConfig
{
    public P95Config() => AddColumn(StatisticColumn.P95);
}
