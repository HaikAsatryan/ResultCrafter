namespace ResultCrafter.AspNetCore;

/// <summary>
///     Sentinel registered by <see cref="ServiceCollectionExtensions.AddResultCrafter" />.
///     <see cref="ApplicationBuilderExtensions.UseResultCrafter" /> marks it as configured.
///     <see cref="ResultCrafterStartupValidator" /> checks both flags on application startup.
/// </summary>
internal sealed class ResultCrafterMarker
{
    public bool MiddlewareConfigured { get; set; }
}
