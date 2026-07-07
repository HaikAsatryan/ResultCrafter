using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using ResultCrafter.AspNetCore;
using ResultCrafter.AspNetCore.EfCore;

namespace ResultCrafter.Tests.AspNetCore;

/// <summary>
///     Guards the documented exception-handler ordering rule: the specific handlers
///     (<see cref="BadHttpRequestExceptionHandler" /> and the EF Core concurrency handler)
///     must be registered before the catch-all <see cref="ResultCrafterExceptionHandler" />,
///     because <c>UseExceptionHandler</c> consults <see cref="IExceptionHandler" /> services
///     in registration order.
/// </summary>
public sealed class EfCoreHandlerOrderingTests
{
    // The EF Core handler is internal to ResultCrafter.AspNetCore.EfCore, so it is matched by name.
    private const string EfCoreHandlerFullName =
        "ResultCrafter.AspNetCore.EfCore.EfCoreConcurrencyExceptionHandler";

    [Fact]
    public void AddResultCrafterEfCore_DescriptorOrder_SpecificHandlersPrecedeCatchAll()
    {
        var services = BuildServices();

        var handlerTypeNames = services
            .Where(d => d.ServiceType == typeof(IExceptionHandler))
            .Select(d => d.ImplementationType?.FullName)
            .ToList();

        var badRequestIndex = handlerTypeNames.IndexOf(typeof(BadHttpRequestExceptionHandler).FullName);
        var efCoreIndex = handlerTypeNames.IndexOf(EfCoreHandlerFullName);
        var catchAllIndex = handlerTypeNames.IndexOf(typeof(ResultCrafterExceptionHandler).FullName);

        Assert.True(badRequestIndex >= 0, "BadHttpRequestExceptionHandler should be registered.");
        Assert.True(efCoreIndex >= 0, "EfCoreConcurrencyExceptionHandler should be registered.");
        Assert.True(catchAllIndex >= 0, "ResultCrafterExceptionHandler should be registered.");
        Assert.True(badRequestIndex < catchAllIndex,
            "The BadHttpRequest handler must precede the catch-all 500 handler.");
        Assert.True(efCoreIndex < catchAllIndex,
            "The EF Core concurrency handler must precede the catch-all 500 handler.");
    }

    [Fact]
    public void AddResultCrafterEfCore_ResolvedHandlers_CatchAllComesLast()
    {
        var services = BuildServices();
        var provider = services.BuildServiceProvider();

        var handlerTypeNames = provider.GetServices<IExceptionHandler>()
            .Select(h => h.GetType().FullName)
            .ToList();

        Assert.Equal(3, handlerTypeNames.Count);
        Assert.Equal(typeof(ResultCrafterExceptionHandler).FullName, handlerTypeNames[^1]);
        Assert.Contains(typeof(BadHttpRequestExceptionHandler).FullName, handlerTypeNames);
        Assert.Contains(EfCoreHandlerFullName, handlerTypeNames);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static ServiceCollection BuildServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        // IHostEnvironment is required by ResultCrafterExceptionHandler — register a stub.
        services.AddSingleton<IHostEnvironment>(new StubHostEnvironment());
        services.AddResultCrafter();
        services.AddResultCrafterEfCore();
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
