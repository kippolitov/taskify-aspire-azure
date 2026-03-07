using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Taskify.Api.Data;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection") // Azure Container Apps
    ?? builder.Configuration.GetConnectionString("taskifydb") // Local Aspire
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' or 'taskifydb' was not found.");

builder.Services.AddDbContext<TaskifyDbContext>(options =>
    options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention()
);

using var host = builder.Build();

var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Taskify.Migrator");

const int maxAttempts = 12;
var delay = TimeSpan.FromSeconds(2);

for (var attempt = 1; attempt <= maxAttempts; attempt++)
{
    try
    {
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TaskifyDbContext>();

        logger.LogInformation("Applying EF migrations (attempt {Attempt}/{MaxAttempts})...", attempt, maxAttempts);
        await db.Database.MigrateAsync();
        logger.LogInformation("EF migrations applied successfully.");
        return;
    }
    catch (Exception ex) when (attempt < maxAttempts)
    {
        logger.LogWarning(
            ex,
            "EF migration attempt {Attempt}/{MaxAttempts} failed; retrying in {DelaySeconds}s.",
            attempt,
            maxAttempts,
            delay.TotalSeconds
        );
        await Task.Delay(delay);
    }
}

throw new InvalidOperationException(
    $"EF migrations failed after {maxAttempts} attempts. See logs for details."
);