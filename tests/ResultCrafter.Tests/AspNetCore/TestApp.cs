using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;

namespace ResultCrafter.Tests.AspNetCore;

/// <summary>
///     Builds an in-memory <see cref="WebApplication" /> backed by the ASP.NET Core
///     <c>TestServer</c> so exception handlers and endpoint filters can be exercised against
///     real requests routed through the full middleware pipeline.
/// </summary>
internal static class TestApp
{
    public static WebApplication Build(Action<WebApplicationBuilder> configure,
        Action<WebApplication> configureApp,
        string environment = "Production")
    {
        // Explicit environment: binding failures throw in Development and do not in Production,
        // so tests that care must not depend on the machine's ASPNETCORE_ENVIRONMENT.
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        configure(builder);

        var app = builder.Build();
        configureApp(app);
        return app;
    }
}
