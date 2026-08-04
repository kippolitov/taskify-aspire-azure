var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres").AddDatabase("taskifydb");

var migrate = builder
    .AddProject<Projects.Taskify_Migrator>("taskify-migrate")
    .WithReference(postgres)
    .WaitFor(postgres);

var api = builder
    .AddProject<Projects.Taskify_Api>("taskify-api", launchProfileName: "http")
    .WithReference(postgres)
    .WaitFor(postgres)
    .WaitForCompletion(migrate);

builder
    .AddProject<Projects.Taskify_Web>("taskify-web", launchProfileName: "http")
    .WithReference(api)
    .WaitFor(api)
    .WaitForCompletion(migrate);

// Local-only observability stack: scrapes /metrics from the "http" launch profile ports
// above (5271, 5237) via host.docker.internal. Excluded from any manifest/deploy output.
var prometheus = builder
    .AddContainer("prometheus", "prom/prometheus")
    .WithBindMount("prometheus", "/etc/prometheus", isReadOnly: true)
    .WithArgs("--config.file=/etc/prometheus/prometheus.yml")
    .WithHttpEndpoint(port: 9090, targetPort: 9090)
    .ExcludeFromManifest();

builder
    .AddContainer("grafana", "grafana/grafana")
    .WithBindMount("grafana/config/provisioning", "/etc/grafana/provisioning", isReadOnly: true)
    .WithBindMount("grafana/dashboards", "/var/lib/grafana/dashboards", isReadOnly: true)
    .WithEnvironment("GF_SECURITY_ADMIN_USER", "admin")
    .WithEnvironment("GF_SECURITY_ADMIN_PASSWORD", "admin")
    .WithHttpEndpoint(port: 3000, targetPort: 3000)
    .WaitFor(prometheus)
    .ExcludeFromManifest();

await builder.Build().RunAsync();
