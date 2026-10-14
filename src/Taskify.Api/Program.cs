using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Taskify.Api.Data;
using Taskify.Api.Endpoints;
using Taskify.Api.ErrorHandling;
using Taskify.Api.Hubs;
using Taskify.Api.Identity;
using Taskify.Api.Observability;
using Taskify.Api.Services;
using Taskify.Shared;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddNpgsqlDbContext<TaskifyDbContext>(
    "taskifydb",
    configureDbContextOptions: options => options.UseSnakeCaseNamingConvention()
);

builder.Services.AddScoped<TaskService>();
builder.Services.AddScoped<CommentService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<TaskifyMetrics>();

builder.Services.ConfigureHttpJsonOptions(o => TaskifyJson.Configure(o.SerializerOptions));

// A body that cannot be read is a 400 in every environment, written by UseStatusCodePages.
builder.Services.Configure<RouteHandlerOptions>(o => o.ThrowOnBadRequest = false);
builder.Services.AddValidation();
builder.Services.AddTaskifyContractValidation();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<UnexpectedExceptionHandler>();

builder
    .Services.AddOptions<AuthOptions>()
    .BindConfiguration(AuthOptions.Section)
    .Validate(
        o => o.IsValid(),
        "Auth needs an Issuer, an Audience and a SigningKey of at least 32 bytes, base64-encoded."
    )
    .ValidateOnStart();
builder.Services.AddSingleton<TokenIssuer>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder
    .Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<AuthOptions>, TimeProvider>(
        (jwt, auth, time) =>
        {
            // Read "sub" as-is instead of mapping it to a long claim type name.
            jwt.MapInboundClaims = false;
            jwt.TokenValidationParameters = auth.Value.CreateValidationParameters(time);
        }
    );
builder.Services.AddAuthorization();

var signalR = builder
    .Services.AddSignalR()
    .AddJsonProtocol(o => TaskifyJson.Configure(o.PayloadSerializerOptions));

// With Redis, live updates reach viewers on every API instance (FR-032). Readiness does not
// depend on it: a Redis outage loses live updates, never saved changes.
var redisConnectionString = builder.Configuration.GetConnectionString("redis");
if (!string.IsNullOrEmpty(redisConnectionString))
{
    signalR.AddStackExchangeRedis(
        redisConnectionString,
        o => o.Configuration.ChannelPrefix = RedisChannel.Literal("taskify")
    );
}

builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapDefaultEndpoints();
app.MapAuthEndpoints();
app.MapUserEndpoints();
app.MapProjectEndpoints();
app.MapTaskEndpoints();
app.MapCommentEndpoints();
app.MapNotificationEndpoints();
app.MapHub<TaskifyHub>("/hubs/taskify");

await app.RunAsync();

// Makes the entry point visible to WebApplicationFactory<Program>; it cannot be static.
#pragma warning disable S1118
public partial class Program;
#pragma warning restore S1118
