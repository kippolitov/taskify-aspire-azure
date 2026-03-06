using Taskify.Web.Components;
using Taskify.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Razor / Blazor Server
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// Identity service (scoped per circuit / user session)
builder.Services.AddScoped<IdentityService>();

// Typed HTTP client (service-discovery-aware)
// https+http:// is the Aspire service-discovery scheme — not a literal hardcoded URL
#pragma warning disable S1075
const string ApiServiceName = "https+http://taskify-api";
#pragma warning restore S1075

builder
    .Services.AddHttpClient<ApiClient>(c => c.BaseAddress = new Uri(ApiServiceName))
    .AddServiceDiscovery();

// Named HTTP client used by BoardHubClient via IHttpMessageHandlerFactory
builder
    .Services.AddHttpClient("taskify-api-hub", c => c.BaseAddress = new Uri(ApiServiceName))
    .AddServiceDiscovery();

// SignalR board hub client (scoped per circuit)
builder.Services.AddScoped<BoardHubClient>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapDefaultEndpoints();
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

await app.RunAsync();
