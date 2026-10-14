using System.Text.Json;
using Taskify.Shared;

namespace Taskify.Api.Tests.Infrastructure;

/// <summary>The API's wire format: the shared Taskify configuration.</summary>
public static class TestJson
{
    public static JsonSerializerOptions Options => TaskifyJson.Options;
}
