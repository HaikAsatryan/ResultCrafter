using Microsoft.Extensions.Hosting;

namespace ResultCrafter.AspNetCore.DependencyInjection;

/// <summary>
///     Validates that both <c>AddResultCrafter()</c> and <c>UseResultCrafter()</c> were
///     called. Throws <see cref="InvalidOperationException" /> at startup if either is missing,
///     so misconfiguration is caught before any request is served.
/// </summary>
internal sealed class ResultCrafterStartupValidator(ResultCrafterMarker marker) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!marker.MiddlewareConfigured)
            throw new InvalidOperationException(
                "AddResultCrafter() was called but UseResultCrafter() was not. " +
                "Call app.UseResultCrafter() in your middleware pipeline.");

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
