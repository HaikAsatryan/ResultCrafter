using FluentValidation;
using ResultCrafter.AspNetCore.DependencyInjection;
using ResultCrafter.AspNetCore.EfCore;
using ResultCrafter.Demo;
using ResultCrafter.MediatR;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services
       .AddResultCrafter()
       .AddResultCrafterEfCore();

builder.Services.AddOpenApi();

// Controllers — used by the MVC demo alongside Minimal API endpoints.
// AddResultCrafter() does not require controllers; this is needed only for the
// controller demo in ResultCrafter.Demo.
builder.Services.AddControllers();

builder.Services.AddSingleton<ItemService>();
builder.Services.AddValidatorsFromAssemblyContaining<Program>(ServiceLifetime.Singleton); // Only for demo not needed.

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