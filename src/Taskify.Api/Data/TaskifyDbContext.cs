using Microsoft.EntityFrameworkCore;
using Taskify.Api.Data.Entities;
using Taskify.Shared.Enums;

namespace Taskify.Api.Data;

public class TaskifyDbContext(DbContextOptions<TaskifyDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<TaskItem> TaskItems => Set<TaskItem>();
    public DbSet<Comment> Comments => Set<Comment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── User ──────────────────────────────────────────────────────────
        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.Property(u => u.DisplayName).HasMaxLength(100).IsRequired();
            e.HasIndex(u => u.DisplayName).IsUnique();
            e.Property(u => u.Role).IsRequired();
        });

        // ── Project ───────────────────────────────────────────────────────
        modelBuilder.Entity<Project>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Name).HasMaxLength(200).IsRequired();
            e.HasIndex(p => p.Name).IsUnique();
            e.Property(p => p.Description).HasMaxLength(1000);
            e.Property(p => p.CreatedAt).IsRequired();
        });

        // ── TaskItem ──────────────────────────────────────────────────────
        modelBuilder.Entity<TaskItem>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Title).HasMaxLength(300).IsRequired();
            e.Property(t => t.Description).HasMaxLength(4000);
            e.Property(t => t.Status).IsRequired();
            e.Property(t => t.CreatedAt).IsRequired();
            e.Property(t => t.UpdatedAt).IsRequired();

            e.HasIndex(t => t.ProjectId);
            e.HasIndex(t => t.AssigneeId);

            e.HasOne(t => t.Project)
                .WithMany(p => p.Tasks)
                .HasForeignKey(t => t.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(t => t.Assignee)
                .WithMany(u => u.AssignedTasks)
                .HasForeignKey(t => t.AssigneeId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);
        });

        // ── Comment ───────────────────────────────────────────────────────
        modelBuilder.Entity<Comment>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Text).HasMaxLength(10000).IsRequired();
            e.Property(c => c.CreatedAt).IsRequired();

            e.HasIndex(c => c.TaskItemId);
            e.HasIndex(c => c.AuthorId);

            e.HasOne(c => c.TaskItem)
                .WithMany(t => t.Comments)
                .HasForeignKey(c => c.TaskItemId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(c => c.Author)
                .WithMany(u => u.Comments)
                .HasForeignKey(c => c.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ── Seed ──────────────────────────────────────────────────────────
        var seedDate = new DateTimeOffset(2026, 3, 5, 12, 0, 0, TimeSpan.Zero);

        modelBuilder
            .Entity<User>()
            .HasData(
                new User
                {
                    Id = 1,
                    DisplayName = "Jordan Rivera",
                    Role = UserRole.ProductManager,
                },
                new User
                {
                    Id = 2,
                    DisplayName = "Alex Chen",
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
                    DisplayName = "Marcus Johnson",
                    Role = UserRole.Engineer,
                },
                new User
                {
                    Id = 5,
                    DisplayName = "Sofia Lindqvist",
                    Role = UserRole.Engineer,
                }
            );

        modelBuilder
            .Entity<Project>()
            .HasData(
                new Project
                {
                    Id = 1,
                    Name = "Mobile Relaunch",
                    Description = "Redesign and re-platform the mobile app experience",
                    CreatedAt = seedDate,
                },
                new Project
                {
                    Id = 2,
                    Name = "API Gateway v2",
                    Description = "Build the next-generation internal API gateway",
                    CreatedAt = seedDate,
                },
                new Project
                {
                    Id = 3,
                    Name = "Design System",
                    Description = "Establish shared UI component library and tokens",
                    CreatedAt = seedDate,
                }
            );

        modelBuilder
            .Entity<TaskItem>()
            .HasData(
                new TaskItem
                {
                    Id = 1,
                    ProjectId = 1,
                    Title = "Define new navigation structure",
                    Status = ColumnStatus.Done,
                    AssigneeId = 1,
                    CreatedAt = seedDate,
                    UpdatedAt = seedDate,
                },
                new TaskItem
                {
                    Id = 2,
                    ProjectId = 1,
                    Title = "Implement bottom tab bar",
                    Status = ColumnStatus.InReview,
                    AssigneeId = 2,
                    CreatedAt = seedDate,
                    UpdatedAt = seedDate,
                },
                new TaskItem
                {
                    Id = 3,
                    ProjectId = 1,
                    Title = "Auth flow redesign",
                    Status = ColumnStatus.InProgress,
                    AssigneeId = 3,
                    CreatedAt = seedDate,
                    UpdatedAt = seedDate,
                },
                new TaskItem
                {
                    Id = 4,
                    ProjectId = 1,
                    Title = "Accessibility audit",
                    Status = ColumnStatus.InProgress,
                    AssigneeId = 4,
                    CreatedAt = seedDate,
                    UpdatedAt = seedDate,
                },
                new TaskItem
                {
                    Id = 5,
                    ProjectId = 1,
                    Title = "Beta release preparation",
                    Status = ColumnStatus.ToDo,
                    AssigneeId = null,
                    CreatedAt = seedDate,
                    UpdatedAt = seedDate,
                },
                new TaskItem
                {
                    Id = 6,
                    ProjectId = 2,
                    Title = "Route configuration schema",
                    Status = ColumnStatus.InReview,
                    AssigneeId = 2,
                    CreatedAt = seedDate,
                    UpdatedAt = seedDate,
                },
                new TaskItem
                {
                    Id = 7,
                    ProjectId = 2,
                    Title = "Rate limiting middleware",
                    Status = ColumnStatus.InProgress,
                    AssigneeId = 5,
                    CreatedAt = seedDate,
                    UpdatedAt = seedDate,
                },
                new TaskItem
                {
                    Id = 8,
                    ProjectId = 2,
                    Title = "Load test report",
                    Status = ColumnStatus.ToDo,
                    AssigneeId = null,
                    CreatedAt = seedDate,
                    UpdatedAt = seedDate,
                },
                new TaskItem
                {
                    Id = 9,
                    ProjectId = 3,
                    Title = "Color token definition",
                    Status = ColumnStatus.Done,
                    AssigneeId = 1,
                    CreatedAt = seedDate,
                    UpdatedAt = seedDate,
                },
                new TaskItem
                {
                    Id = 10,
                    ProjectId = 3,
                    Title = "Button component",
                    Status = ColumnStatus.InProgress,
                    AssigneeId = 3,
                    CreatedAt = seedDate,
                    UpdatedAt = seedDate,
                }
            );
    }
}
