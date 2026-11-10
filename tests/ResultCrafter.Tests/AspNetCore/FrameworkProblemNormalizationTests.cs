using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ResultCrafter.AspNetCore;
using ResultCrafter.AspNetCore.ProblemDetails;

namespace ResultCrafter.Tests.AspNetCore;

/// <summary>
///     Unit tests for the framework-default ProblemDetails normalization installed by
///     <c>AddResultCrafter()</c> (ConfigureResultCrafterProblemDetails). Regression guard for the
///     bug where bare responses with a null title skipped normalization: ASP.NET Core's
///     <c>ProblemDetailsDefaults</c> table has no 429 entry, so the framework leaves its <c>Title</c>
///     null, and the old reason-phrase-equality guard treated a null <c>Title</c> as "already
///     customized" and bailed out.
/// </summary>
public sealed class FrameworkProblemNormalizationTests
{
    private static Action<ProblemDetailsContext> BuildCustomizer()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddResultCrafter();

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<ProblemDetailsOptions>>().Value;

        return options.CustomizeProblemDetails
               ?? throw new InvalidOperationException("CustomizeProblemDetails was not configured.");
    }

    private static Microsoft.AspNetCore.Mvc.ProblemDetails Normalize(int status, string? title)
    {
        var pd = new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = status, Title = title };
        Customize(pd);
        return pd;
    }

    private static void Customize(Microsoft.AspNetCore.Mvc.ProblemDetails pd)
    {
        var context = new ProblemDetailsContext
        {
            HttpContext = new DefaultHttpContext(),
            ProblemDetails = pd
        };

        BuildCustomizer().Invoke(context);
    }

    // A null title (429 has no ProblemDetailsDefaults entry; 410 covers a problem built without one) must
    // still normalize: these were NOT normalized before the null-title branch was added.
    [Theory]
    [InlineData(410, "gone", "resource_gone")]
    [InlineData(429, "too_many_requests", "too_many_requests")]
    public void BareResponse_WithNullTitle_IsNormalizedToHouseStyle(int status, string expectedTitle,
        string expectedDetail)
    {
        var pd = Normalize(status, null);

        Assert.True(HttpErrorCatalog.TryGetErrorType(status, out var type));
        Assert.Equal(expectedTitle, pd.Title);
        Assert.Equal(expectedDetail, pd.Detail);
        Assert.Equal(HttpErrorCatalog.TypeUri(type), pd.Type);
    }

    // Control: statuses that ARE in ProblemDetailsDefaults arrive with Title == reason phrase and
    // must keep normalizing exactly as before (proves the null-title branch didn't regress them).
    [Theory]
    [InlineData(400, "Bad Request", "bad_request", "the_request_was_invalid_or_cannot_be_otherwise_served")]
    [InlineData(404, "Not Found", "not_found", "resource_not_found")]
    public void FrameworkDefaultTitle_IsNormalizedToHouseStyle(int status, string frameworkTitle,
        string expectedTitle, string expectedDetail)
    {
        var pd = Normalize(status, frameworkTitle);

        Assert.True(HttpErrorCatalog.TryGetErrorType(status, out var type));
        Assert.Equal(expectedTitle, pd.Title);
        Assert.Equal(expectedDetail, pd.Detail);
        Assert.Equal(HttpErrorCatalog.TypeUri(type), pd.Type);
    }

    // Normalization fills a missing detail; it must not replace one the application wrote.
    [Fact]
    public void FrameworkDefaultTitle_WithOwnDetail_KeepsTheDetail()
    {
        var pd = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = 404,
            Title = "Not Found",
            Detail = "Item 5 is archived."
        };

        Customize(pd);

        Assert.Equal("not_found", pd.Title);
        Assert.Equal("Item 5 is archived.", pd.Detail);
    }

    // AddResultCrafter() chains onto the app's own hook instead of replacing it. The app's hook runs first, so it
    // still sees the internal markers it may branch on, before they are stripped.
    [Fact]
    public void AppCustomizeProblemDetails_StillRuns_BeforeMarkersAreStripped()
    {
        var sawMarker = false;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
        {
            sawMarker = ctx.ProblemDetails.Extensions.ContainsKey(ProblemDetailsKeys.RcMarker);
            ctx.ProblemDetails.Extensions["tenant"] = "acme";
        });
        services.AddResultCrafter();
        var customize = services.BuildServiceProvider()
            .GetRequiredService<IOptions<ProblemDetailsOptions>>().Value.CustomizeProblemDetails!;
        var pd = ProblemDetailsBuilder.Build(Error.NotFound());

        customize(new ProblemDetailsContext { HttpContext = new DefaultHttpContext(), ProblemDetails = pd });

        Assert.True(sawMarker);
        Assert.Equal("acme", pd.Extensions["tenant"]);
        Assert.False(pd.Extensions.ContainsKey(ProblemDetailsKeys.RcMarker));
    }

    // A non-default, non-empty title (set by a Result response or the application) must be preserved.
    [Fact]
    public void CustomTitle_OnMappedStatus_IsPreserved()
    {
        var pd = Normalize(404, "resource_was_deliberately_named");

        Assert.Equal("resource_was_deliberately_named", pd.Title);
    }

    // A status the catalog does not map (418) is left entirely untouched.
    [Fact]
    public void UnmappedStatus_IsLeftUnchanged()
    {
        var pd = Normalize(418, null);

        Assert.Null(pd.Title);
        Assert.Null(pd.Detail);
    }

    // MVC can report a JSON path next to a model key. Neither the JSON body rule nor the field name rule
    // fits both, so the framework response must stay exactly as it is.
    [Fact]
    public void ValidationProblem_WithMixedJsonPathAndFieldKeys_IsLeftUnchanged()
    {
        var pd = new HttpValidationProblemDetails(new Dictionary<string, string[]>
        {
            ["$.count"] = ["The JSON value could not be converted to System.Int32."],
            ["Request"] = ["The Request field is required."]
        })
        {
            Status = StatusCodes.Status400BadRequest
        };
        var frameworkTitle = new HttpValidationProblemDetails().Title;

        Customize(pd);

        Assert.Equal(frameworkTitle, pd.Title);
        Assert.Null(pd.Detail);
        Assert.Equal(["$.count", "Request"], pd.Errors.Keys.Order(StringComparer.Ordinal));
        Assert.Equal("The JSON value could not be converted to System.Int32.", Assert.Single(pd.Errors["$.count"]));
    }

    // The framework's table points 422 at RFC 4918; the house type is RFC 9110, as for Error.UnprocessableEntity().
    [Fact]
    public void Bare422_WithFrameworkTypeUri_GetsTheHouseTypeUri()
    {
        var pd = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = 422,
            Title = "Unprocessable Entity",
            Type = "https://tools.ietf.org/html/rfc4918#section-11.2"
        };

        Customize(pd);

        Assert.Equal(HttpErrorCatalog.TypeUri(ErrorType.UnprocessableEntity), pd.Type);
        Assert.Equal("unprocessable_entity", pd.Title);
    }

    [Fact]
    public void BareResponse_WithCustomTypeUri_KeepsIt()
    {
        var pd = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = 404,
            Title = "Not Found",
            Type = "https://example.com/problems/missing"
        };

        Customize(pd);

        Assert.Equal("https://example.com/problems/missing", pd.Type);
    }
}
