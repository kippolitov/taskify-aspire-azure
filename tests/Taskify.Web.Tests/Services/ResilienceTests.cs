using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace Taskify.Web.Tests.Services;

/// <summary>
/// The shared HTTP defaults retry failed reads, but never resend a change, which could
/// apply it twice (FR-037, SC-008).
/// </summary>
public class ResilienceTests
{
    private sealed class AlwaysUnavailable : HttpMessageHandler
    {
        private int _calls;

        public int Calls => _calls;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Interlocked.Increment(ref _calls);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        }
    }

    private static (HttpClient Client, AlwaysUnavailable Handler) CreateClient()
    {
        var handler = new AlwaysUnavailable();
        var builder = Host.CreateApplicationBuilder();
        builder.AddServiceDefaults();
        builder.Services.ConfigureAll<HttpStandardResilienceOptions>(o =>
        {
            o.Retry.Delay = TimeSpan.FromMilliseconds(10);
            o.Retry.BackoffType = DelayBackoffType.Constant;
            o.Retry.UseJitter = false;
        });
        builder
            .Services.AddHttpClient("probe", c => c.BaseAddress = new Uri("http://localhost/"))
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        var services = builder.Build().Services;
        return (services.GetRequiredService<IHttpClientFactory>().CreateClient("probe"), handler);
    }

    [Fact]
    public async Task FailingPost_IsSentExactlyOnce()
    {
        var (client, handler) = CreateClient();

        var response = await client.PostAsync("api/tasks", content: null);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task FailingGet_IsRetried()
    {
        var (client, handler) = CreateClient();

        var response = await client.GetAsync("api/tasks");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.True(handler.Calls > 1, $"Expected retries, got {handler.Calls} call(s).");
    }
}
