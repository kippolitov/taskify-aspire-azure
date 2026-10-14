using Microsoft.AspNetCore.Hosting;

namespace Taskify.Api.Tests.Infrastructure;

/// <summary>The settings every in-memory API host needs.</summary>
public static class TestSettings
{
    /// <summary>A fixed 32-byte key, base64-encoded. For tests only.</summary>
    public static readonly string SigningKey = Convert.ToBase64String(
        Enumerable.Range(1, 32).Select(i => (byte)i).ToArray()
    );

    public const string Issuer = "taskify-tests";
    public const string Audience = "taskify-api";

    public static IWebHostBuilder UseTaskifyTestSettings(
        this IWebHostBuilder builder,
        string connectionString
    ) =>
        builder
            .UseEnvironment("Development")
            .UseSetting("ConnectionStrings:taskifydb", connectionString)
            .UseSetting("Auth:SigningKey", SigningKey)
            .UseSetting("Auth:Issuer", Issuer)
            .UseSetting("Auth:Audience", Audience);
}
