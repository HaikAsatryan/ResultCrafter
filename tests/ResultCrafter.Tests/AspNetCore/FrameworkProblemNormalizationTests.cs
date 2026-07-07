using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ResultCrafter.AspNetCore;
using ResultCrafter.AspNetCore.ProblemDetails;

namespace ResultCrafter.Tests.AspNetCore;

/// <summary>
///     Unit tests for the framework-default ProblemDetails normalization installed by
///     <c>AddResultCrafter()</c> (ConfigureResultCrafterProblemDetails). Regression guard for the
///     bug where bare <c>410</c>/<c>429</c> responses skipped normalization: ASP.NET Core's
///     <c>ProblemDetailsDefaults</c> table has no 410/429 entry, so the framework leaves their
///     <c>Title</c> null, and the old reason-phrase-equality guard treated a null <c>Title</c> as
///     "already customized" and bailed out, breaking dev/prod house-style parity for exactly the two
///     error types 2.0 added (Gone, TooManyRequests).
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

    private static ProblemDetails Normalize(int status, string? title)
    {
        var pd = new ProblemDetails { Status = status, Title = title };
        var context = new ProblemDetailsContext
        {
            HttpContext = new DefaultHttpContext(),
            ProblemDetails = pd
        };

        BuildCustomizer().Invoke(context);
        return pd;
    }

    // 410 and 429 are absent from ProblemDetailsDefaults, so the framework leaves Title null. These
    // two are the regression: they were NOT normalized before the null-title branch was added.
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
}
