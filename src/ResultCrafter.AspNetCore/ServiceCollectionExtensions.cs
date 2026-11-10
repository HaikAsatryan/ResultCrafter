using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ResultCrafter.AspNetCore;

/// <summary>
///     Extension methods for registering ResultCrafter services with the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Registers ResultCrafter services:
    ///     <list type="bullet">
    ///         <item>RFC 9457 ProblemDetails with automatic enrichment and 4xx structured logging.</item>
    ///         <item>
    ///             <see cref="BadHttpRequestExceptionHandler" /> for <see cref="BadHttpRequestException" />
    ///             (e.g. Minimal API binding failures), mapping it to its carried 4xx status.
    ///         </item>
    ///         <item><see cref="ResultCrafterExceptionHandler" /> for unhandled 5xx exceptions.</item>
    ///         <item>
    ///             A JSON converter in the Minimal API and MVC JSON options that throws when a
    ///             <see cref="Result{T}" />, <see cref="Result" /> or <see cref="Error" /> is returned from an
    ///             endpoint without being mapped, instead of sending it as JSON with status 200.
    ///         </item>
    ///     </list>
    ///     Call <see cref="ApplicationBuilderExtensions.UseResultCrafter" /> in your middleware
    ///     pipeline after calling this method.
    /// </summary>
    public static IServiceCollection AddResultCrafter(this IServiceCollection services,
        Action<ResultCrafterOptions>? configure = null)
    {
        if (configure is not null) services.Configure(configure);

        services.AddProblemDetails();
        services.AddSingleton<IPostConfigureOptions<ProblemDetailsOptions>,
            ConfigureResultCrafterProblemDetails>();

        // First in line, so ResultCrafter errors keep the house body whether AddControllers() ran before or
        // after this call and whatever the Accept header. Problems without the x-rc marker go to the next writer.
        if (!services.Any(d => d.ImplementationType == typeof(ResultCrafterProblemDetailsWriter)))
            services.Insert(0, ServiceDescriptor.Singleton<IProblemDetailsWriter, ResultCrafterProblemDetailsWriter>());

        services.ConfigureHttpJsonOptions(o =>
            o.SerializerOptions.Converters.Add(UnmappedResultJsonConverterFactory.Instance));
        services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(o =>
            o.JsonSerializerOptions.Converters.Add(UnmappedResultJsonConverterFactory.Instance));

        // Order matters: exception handlers are consulted in registration order. The
        // BadHttpRequestException handler must precede the catch-all so 4xx binding failures
        // are not swallowed as 500s.
        services.AddExceptionHandler<BadHttpRequestExceptionHandler>();
        services.AddExceptionHandler<ResultCrafterExceptionHandler>();
        services.AddSingleton<ResultCrafterMarker>();
        services.AddHostedService<ResultCrafterStartupValidator>();

        return services;
    }
}
