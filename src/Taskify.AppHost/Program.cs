var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres").AddDatabase("taskifydb");

var api = builder
    .AddProject<Projects.Taskify_Api>("taskify-api")
    .WithReference(postgres)
    .WaitFor(postgres);

builder.AddProject<Projects.Taskify_Web>("taskify-web").WithReference(api).WaitFor(api);

await builder.Build().RunAsync();
