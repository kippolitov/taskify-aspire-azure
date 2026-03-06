var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres").AddDatabase("taskifydb");

var migrate = builder
    .AddProject<Projects.Taskify_Migrator>("taskify-migrate")
    .WithReference(postgres)
    .WaitFor(postgres);

var api = builder
    .AddProject<Projects.Taskify_Api>("taskify-api")
    .WithReference(postgres)
    .WaitFor(postgres)
    .WaitForCompletion(migrate);

builder
    .AddProject<Projects.Taskify_Web>("taskify-web")
    .WithReference(api)
    .WaitFor(api)
    .WaitForCompletion(migrate);

await builder.Build().RunAsync();
