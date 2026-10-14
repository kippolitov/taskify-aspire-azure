using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Taskify.Api.Data;
using Taskify.Migrator;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection") // Azure Container Apps
    ?? builder.Configuration.GetConnectionString("taskifydb") // Local Aspire
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' or 'taskifydb' was not found."
    );

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

        MigratorLog.Applying(logger, attempt, maxAttempts);
        await db.Database.MigrateAsync();
        MigratorLog.Applied(logger);
        return;
    }
    catch (Exception ex) when (attempt < maxAttempts)
    {
        MigratorLog.AttemptFailed(logger, attempt, maxAttempts, delay.TotalSeconds, ex);
        await Task.Delay(delay);
    }
}

throw new InvalidOperationException(
    $"EF migrations failed after {maxAttempts} attempts. See logs for details."
);

namespace Taskify.Migrator
{
    internal static partial class MigratorLog
    {
        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Applying EF migrations (attempt {Attempt}/{MaxAttempts})..."
        )]
        public static partial void Applying(ILogger logger, int attempt, int maxAttempts);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "EF migrations applied successfully."
        )]
        public static partial void Applied(ILogger logger);

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "EF migration attempt {Attempt}/{MaxAttempts} failed; retrying in {DelaySeconds}s."
        )]
        public static partial void AttemptFailed(
            ILogger logger,
            int attempt,
            int maxAttempts,
            double delaySeconds,
            Exception exception
        );
    }
}
