using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using ResultCrafter.AspNetCore;
using ResultCrafter.AspNetCore.ProblemDetails;

namespace ResultCrafter.Tests.AspNetCore;

/// <summary>
///     Each Minimal API <c>Produces*</c> helper must document the house error body under its own status code.
///     A wrong status here would only show up as a wrong OpenAPI document.
/// </summary>
public sealed class RouteHandlerBuilderExtensionsTests
{
    [Theory]
    [InlineData(nameof(RouteHandlerBuilderExtensions.ProducesBadRequest), StatusCodes.Status400BadRequest)]
    [InlineData(nameof(RouteHandlerBuilderExtensions.ProducesUnauthorized), StatusCodes.Status401Unauthorized)]
    [InlineData(nameof(RouteHandlerBuilderExtensions.ProducesForbidden), StatusCodes.Status403Forbidden)]
    [InlineData(nameof(RouteHandlerBuilderExtensions.ProducesNotFound), StatusCodes.Status404NotFound)]
    [InlineData(nameof(RouteHandlerBuilderExtensions.ProducesConflict), StatusCodes.Status409Conflict)]
    [InlineData(nameof(RouteHandlerBuilderExtensions.ProducesGone), StatusCodes.Status410Gone)]
    [InlineData(nameof(RouteHandlerBuilderExtensions.ProducesUnprocessableEntity),
        StatusCodes.Status422UnprocessableEntity)]
    [InlineData(nameof(RouteHandlerBuilderExtensions.ProducesTooManyRequests), StatusCodes.Status429TooManyRequests)]
    public async Task Extension_DeclaresProblemResponseForItsStatus(string extension, int expectedStatus)
    {
        await using var app = TestApp.Build(_ => { }, a => Apply(a.MapGet("/", () => "ok"), extension));

        var endpoint = Assert.Single(((IEndpointRouteBuilder)app).DataSources.SelectMany(s => s.Endpoints));
        var problem = Assert.Single(endpoint.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>(),
            m => m.Type == typeof(ProblemResponse));

        Assert.Equal(expectedStatus, problem.StatusCode);
        Assert.Equal("application/problem+json", Assert.Single(problem.ContentTypes));
    }

    private static void Apply(RouteHandlerBuilder builder, string extension)
    {
        _ = extension switch
        {
            nameof(RouteHandlerBuilderExtensions.ProducesBadRequest) => builder.ProducesBadRequest(),
            nameof(RouteHandlerBuilderExtensions.ProducesUnauthorized) => builder.ProducesUnauthorized(),
            nameof(RouteHandlerBuilderExtensions.ProducesForbidden) => builder.ProducesForbidden(),
            nameof(RouteHandlerBuilderExtensions.ProducesNotFound) => builder.ProducesNotFound(),
            nameof(RouteHandlerBuilderExtensions.ProducesConflict) => builder.ProducesConflict(),
            nameof(RouteHandlerBuilderExtensions.ProducesGone) => builder.ProducesGone(),
            nameof(RouteHandlerBuilderExtensions.ProducesUnprocessableEntity) => builder.ProducesUnprocessableEntity(),
            nameof(RouteHandlerBuilderExtensions.ProducesTooManyRequests) => builder.ProducesTooManyRequests(),
            _ => throw new ArgumentOutOfRangeException(nameof(extension), extension, null)
        };
    }
}
