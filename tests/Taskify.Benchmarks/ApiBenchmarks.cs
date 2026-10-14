using BenchmarkDotNet.Attributes;
using Microsoft.EntityFrameworkCore;
using Taskify.Api.Data;
using Taskify.Api.Data.Entities;
using Taskify.Api.Services;
using Taskify.Shared.Enums;
using Testcontainers.PostgreSql;

namespace Taskify.Benchmarks;

/// <summary>
/// BenchmarkDotNet baselines for GET tasks and PATCH status, against PostgreSQL 16
/// in a container, migrated with the production migrations.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
public class ApiBenchmarks
{
    private PostgreSqlContainer _postgres = null!;
    private TaskifyDbContext _db = null!;
    private TaskService _service = null!;
    private int _toggleCounter;

    [GlobalSetup]
    public async Task Setup()
    {
        _postgres = new PostgreSqlBuilder("postgres:16").Build();
        await _postgres.StartAsync();

        var options = new DbContextOptionsBuilder<TaskifyDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .Options;

        _db = new TaskifyDbContext(options);
        await _db.Database.MigrateAsync();

        var seed = DateTimeOffset.UtcNow;

        for (var i = 1; i <= 20; i++)
        {
            _db.TaskItems.Add(
                new TaskItem
                {
                    ProjectId = 1,
                    Title = $"Benchmark task {i}",
                    Status = (ColumnStatus)(i % 4),
                    CreatedAt = seed,
                    UpdatedAt = seed,
                }
            );
        }

        await _db.SaveChangesAsync();

        _service = BenchmarkServices.CreateTaskService(_db);
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _db.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    /// <summary>Benchmark: retrieve all tasks for project 1 (5 seeded + 20 added).</summary>
    [Benchmark]
    public async Task GetProjectTasks() =>
        await _service.GetProjectTasksAsync(1, CancellationToken.None);

    /// <summary>Benchmark: move a task between two statuses (alternating).</summary>
    [Benchmark]
    public async Task MoveTaskStatus()
    {
        var newStatus = (_toggleCounter++ % 2 == 0) ? ColumnStatus.InProgress : ColumnStatus.ToDo;

        await _service.MoveTaskAsync(1, newStatus, CancellationToken.None);
    }
}
