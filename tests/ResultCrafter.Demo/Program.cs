using ResultCrafter.AspNetCore.DependencyInjection;
using ResultCrafter.AspNetCore.EfCore;
using ResultCrafter.AspNetCore.FluentValidation;
using ResultCrafter.Demo;
using ResultCrafter.MediatR;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// AddResultCrafterValidation uses Singleton here because the demo's ItemService is a Singleton that
// injects the validator; the library default is Scoped (matching FluentValidation).
builder.Services
    .AddResultCrafter()
    .AddResultCrafterEfCore()
    .AddResultCrafterValidation(ServiceLifetime.Singleton);

builder.Services.AddOpenApi();

// Controllers are used by the MVC demo alongside the Minimal API endpoints.
// AddResultCrafter() does not require controllers; this is only for the controller demo.
builder.Services.AddControllers();

builder.Services.AddSingleton<ItemService>();

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
    cfg.AddResultCrafterValidation();
});

var app = builder.Build();

app.UseResultCrafter(); // registers UseExceptionHandler() + UseStatusCodePages()

app.MapOpenApi();
app.MapScalarApiReference();
app.MapGet("/", () => Results.Redirect("/scalar"));

// Minimal API endpoints
app.MapGroup("/api")
    .WithTags("Items")
    .MapDemoEndpoints();

app.MapControllers();

app.Run();
