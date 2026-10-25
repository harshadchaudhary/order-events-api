using OrderEvents.Api.Endpoints;
using OrderEvents.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOrderEventsInfrastructure(builder.Configuration);
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

var app = builder.Build();

await app.Services.EnsureDatabaseCreatedAsync();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).WithTags("Health");
app.MapOrderEndpoints();

app.Run();

/// <summary>
/// Exposed so the integration tests can spin the API up with
/// <c>WebApplicationFactory&lt;Program&gt;</c>.
/// </summary>
public partial class Program;
