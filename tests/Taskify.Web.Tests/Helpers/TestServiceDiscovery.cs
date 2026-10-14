using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.ServiceDiscovery;

namespace Taskify.Web.Tests.Helpers;

/// <summary>
/// A real <see cref="ServiceEndpointResolver"/> with no configured services. It finds no
/// address for the API, so <c>BoardHubClient.StartAsync</c> fails fast in bUnit tests.
/// </summary>
public static class TestServiceDiscovery
{
    public static ServiceEndpointResolver CreateResolver() =>
        new ServiceCollection()
            .AddSingleton<IConfiguration>(new ConfigurationBuilder().Build())
            .AddServiceDiscovery()
            .BuildServiceProvider()
            .GetRequiredService<ServiceEndpointResolver>();
}
