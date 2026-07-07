using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using ResultCrafter.AspNetCore;
using ResultCrafter.AspNetCore.EfCore;

namespace ResultCrafter.Tests.AspNetCore;

public sealed class DependencyInjectionTests
{
    // ── UseResultCrafter guard ─────────────────────────────────────────────────

    [Fact]
    public void UseResultCrafter_WhenAddResultCrafterNotCalled_Throws()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var provider = services.BuildServiceProvider();

        var app = new ApplicationBuilder(provider);

        var ex = Assert.Throws<InvalidOperationException>(() => app.UseResultCrafter());
        Assert.Contains("AddResultCrafter", ex.Message);
    }

    // ── AddResultCrafter registers expected services ───────────────────────────

    [Fact]
    public void AddResultCrafter_RegistersResultCrafterMarker()
    {
        var services = BuildServices();

        var provider = services.BuildServiceProvider();
        var marker = provider.GetService<ResultCrafterMarker>();

        Assert.NotNull(marker);
    }

    [Fact]
    public void AddResultCrafter_RegistersExceptionHandler()
    {
        // IHostEnvironment is required by ResultCrafterExceptionHandler — register a stub.
        var services = BuildServices(true);

        var provider = services.BuildServiceProvider();
        var handlers = provider.GetServices<IExceptionHandler>()
            .ToList();

        Assert.NotEmpty(handlers);
    }

    // ── AddResultCrafterEfCore ordering ───────────────────────────────────────

    [Fact]
    public void AddResultCrafterEfCore_WhenCalledBeforeAddResultCrafter_Throws()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var ex = Assert.Throws<InvalidOperationException>(() => services.AddResultCrafterEfCore());

        Assert.Contains("AddResultCrafter", ex.Message);
    }

    [Fact]
    public void AddResultCrafterEfCore_WhenCalledAfterAddResultCrafter_DoesNotThrow()
    {
        var services = BuildServices();

        // Should not throw
        services.AddResultCrafterEfCore();
    }

    [Fact]
    public void AddResultCrafterEfCore_RegistersThreeExceptionHandlers()
    {
        // IHostEnvironment is required by ResultCrafterExceptionHandler — register a stub.
        var services = BuildServices(true);
        services.AddResultCrafterEfCore();

        var provider = services.BuildServiceProvider();
        var handlers = provider.GetServices<IExceptionHandler>()
            .ToList();

        // BadHttpRequest handler + EfCore handler + generic 500 handler
        Assert.Equal(3, handlers.Count);
    }

    [Fact]
    public void AddResultCrafter_RegistersBadHttpRequestHandlerBeforeCatchAll()
    {
        var services = BuildServices();

        var handlerTypes = services
            .Where(d => d.ServiceType == typeof(IExceptionHandler))
            .Select(d => d.ImplementationType)
            .ToList();

        var badRequestIndex = handlerTypes.IndexOf(typeof(BadHttpRequestExceptionHandler));
        var catchAllIndex = handlerTypes.IndexOf(typeof(ResultCrafterExceptionHandler));

        Assert.True(badRequestIndex >= 0, "BadHttpRequestExceptionHandler should be registered.");
        Assert.True(catchAllIndex >= 0, "ResultCrafterExceptionHandler should be registered.");
        Assert.True(badRequestIndex < catchAllIndex,
            "The BadHttpRequest handler must precede the catch-all 500 handler.");
    }

    // ── ResultCrafterStartupValidator ─────────────────────────────────────────

    [Fact]
    public async Task StartupValidator_WhenMiddlewareNotConfigured_Throws()
    {
        var marker = new ResultCrafterMarker(); // MiddlewareConfigured defaults to false
        var validator = new ResultCrafterStartupValidator(marker);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            validator.StartAsync(CancellationToken.None));

        Assert.Contains("UseResultCrafter", ex.Message);
    }

    [Fact]
    public async Task StartupValidator_WhenMiddlewareIsConfigured_DoesNotThrow()
    {
        var marker = new ResultCrafterMarker
        {
            MiddlewareConfigured = true
        };
        var validator = new ResultCrafterStartupValidator(marker);

        await validator.StartAsync(CancellationToken.None);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static ServiceCollection BuildServices(bool withHostEnvironment = false)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddResultCrafter();

        if (withHostEnvironment) services.AddSingleton<IHostEnvironment>(new StubHostEnvironment());

        return services;
    }

    private sealed class StubHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = "/";
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
