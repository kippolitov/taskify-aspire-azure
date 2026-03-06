using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Taskify.Api.Data;
using Taskify.Api.Hubs;
using Taskify.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddNpgsqlDbContext<TaskifyDbContext>(
    "taskifydb",
    configureDbContextOptions: options => options.UseSnakeCaseNamingConvention()
);

builder.Services.AddScoped<TaskService>();
builder.Services.AddScoped<CommentService>();

builder
    .Services.AddControllers()
    .AddJsonOptions(opt =>
    {
        opt.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        opt.JsonSerializerOptions.PropertyNamingPolicy = System
            .Text
            .Json
            .JsonNamingPolicy
            .CamelCase;
    });

builder
    .Services.AddSignalR()
    .AddJsonProtocol(opt =>
    {
        opt.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        opt.PayloadSerializerOptions.PropertyNamingPolicy = System
            .Text
            .Json
            .JsonNamingPolicy
            .CamelCase;
    });

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapDefaultEndpoints();
app.MapControllers();
app.MapHub<TaskifyHub>("/hubs/taskify");

// Apply EF Core migrations on startup -- NEVER EnsureCreatedAsync
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TaskifyDbContext>();
    await db.Database.MigrateAsync();
}

await app.RunAsync();
