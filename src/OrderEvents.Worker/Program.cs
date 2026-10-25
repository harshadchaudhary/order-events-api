using OrderEvents.Infrastructure;
using OrderEvents.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddOrderEventsInfrastructure(builder.Configuration);
builder.Services.AddHostedService<OrderPlacedConsumer>();

var host = builder.Build();

// The API normally creates the schema first; the worker does it too so it can
// be started on its own.
await host.Services.EnsureDatabaseCreatedAsync();

await host.RunAsync();
