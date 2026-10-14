using System.Text.Json;
using System.Text.Json.Serialization;

namespace Taskify.Shared;

/// <summary>
/// The one JSON configuration for the API, the hub and the web app: camelCase,
/// case-insensitive reads, and enums as strings (FR-020).
/// </summary>
public static class TaskifyJson
{
    public static JsonSerializerOptions Options { get; } = Create();

    public static void Configure(JsonSerializerOptions o)
    {
        o.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        o.PropertyNameCaseInsensitive = true;
        o.Converters.Add(new JsonStringEnumConverter());
    }

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Configure(options);
        return options;
    }
}
