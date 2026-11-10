using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using ControllerExtensions = ResultCrafter.AspNetCore.Mvc.ControllerResultExtensions;
using MinimalApiExtensions = ResultCrafter.AspNetCore.MinimalApiResultExtensions;

namespace ResultCrafter.Tests.AspNetCore;

/// <summary>
///     Guards the SuccessKind validation on every <c>To*Result</c> method of both mapping
///     surfaces: a success <see cref="Result{T}" /> / <see cref="Result" /> whose
///     <see cref="SuccessKind" /> does not match the intended HTTP status throws
///     <see cref="InvalidOperationException" />; the matching kind maps cleanly.
/// </summary>
public sealed class SuccessKindGuardTests
{
    private const string Location = "/items/42";

    // ════════════════════════════════════════════════════════
    // Minimal API surface: Result<T>
    // ════════════════════════════════════════════════════════

    [Fact]
    public void MinimalApiToOkResult_CreatedKind_Throws()
    {
        var result = Result<int>.Created(Location, 1);
        Assert.Throws<InvalidOperationException>(() => MinimalApiExtensions.ToOkResult(result));
    }

    [Fact]
    public void MinimalApiToOkResult_AcceptedKind_Throws()
    {
        var result = Result<int>.Accepted(1);
        Assert.Throws<InvalidOperationException>(() => MinimalApiExtensions.ToOkResult(result));
    }

    [Fact]
    public void MinimalApiToOkResult_OkKind_ReturnsOk()
    {
        var mapped = MinimalApiExtensions.ToOkResult(Result<int>.Ok(1));
        Assert.IsType<Ok<int>>(mapped.Result);
    }

    [Fact]
    public void MinimalApiToCreatedResult_OkKind_Throws()
    {
        var result = Result<int>.Ok(1);
        Assert.Throws<InvalidOperationException>(() => MinimalApiExtensions.ToCreatedResult(result));
    }

    [Fact]
    public void MinimalApiToCreatedResult_AcceptedKind_Throws()
    {
        var result = Result<int>.Accepted(1, Location);
        Assert.Throws<InvalidOperationException>(() => MinimalApiExtensions.ToCreatedResult(result));
    }

    [Fact]
    public void MinimalApiToCreatedResult_CreatedKind_ReturnsCreated()
    {
        var mapped = MinimalApiExtensions.ToCreatedResult(Result<int>.Created(Location, 1));
        Assert.IsType<Created<int>>(mapped.Result);
    }

    [Fact]
    public void MinimalApiToAcceptedResultOfT_OkKind_Throws()
    {
        var result = Result<int>.Ok(1);
        Assert.Throws<InvalidOperationException>(() => MinimalApiExtensions.ToAcceptedResult(result));
    }

    [Fact]
    public void MinimalApiToAcceptedResultOfT_CreatedKind_Throws()
    {
        var result = Result<int>.Created(Location, 1);
        Assert.Throws<InvalidOperationException>(() => MinimalApiExtensions.ToAcceptedResult(result));
    }

    [Fact]
    public void MinimalApiToAcceptedResultOfT_AcceptedKind_ReturnsAccepted()
    {
        var mapped = MinimalApiExtensions.ToAcceptedResult(Result<int>.Accepted(1, Location));
        Assert.IsType<Accepted<int>>(mapped.Result);
    }

    // ════════════════════════════════════════════════════════
    // Minimal API surface: void Result
    // ════════════════════════════════════════════════════════

    [Fact]
    public void MinimalApiToNoContentResult_AcceptedKind_Throws()
    {
        var result = Result.Accepted();
        Assert.Throws<InvalidOperationException>(() => MinimalApiExtensions.ToNoContentResult(result));
    }

    [Fact]
    public void MinimalApiToNoContentResult_NoContentKind_ReturnsNoContent()
    {
        var mapped = MinimalApiExtensions.ToNoContentResult(Result.NoContent());
        Assert.IsType<NoContent>(mapped.Result);
    }

    [Fact]
    public void MinimalApiToAcceptedResult_NoContentKind_Throws()
    {
        var result = Result.NoContent();
        Assert.Throws<InvalidOperationException>(() => MinimalApiExtensions.ToAcceptedResult(result));
    }

    [Fact]
    public void MinimalApiToAcceptedResult_AcceptedKind_ReturnsAccepted()
    {
        var mapped = MinimalApiExtensions.ToAcceptedResult(Result.Accepted(Location));
        Assert.IsType<Accepted>(mapped.Result);
    }

    // ════════════════════════════════════════════════════════
    // Controller surface: Result<T>
    // ════════════════════════════════════════════════════════

    [Fact]
    public void ControllerToOkResult_CreatedKind_Throws()
    {
        var result = Result<int>.Created(Location, 1);
        Assert.Throws<InvalidOperationException>(() => ControllerExtensions.ToOkResult(result));
    }

    [Fact]
    public void ControllerToOkResult_AcceptedKind_Throws()
    {
        var result = Result<int>.Accepted(1);
        Assert.Throws<InvalidOperationException>(() => ControllerExtensions.ToOkResult(result));
    }

    [Fact]
    public void ControllerToOkResult_OkKind_ReturnsOkObjectResult()
    {
        var mapped = ControllerExtensions.ToOkResult(Result<int>.Ok(1));
        Assert.IsType<OkObjectResult>(mapped.Result);
    }

    [Fact]
    public void ControllerToCreatedResult_OkKind_Throws()
    {
        var result = Result<int>.Ok(1);
        Assert.Throws<InvalidOperationException>(() => ControllerExtensions.ToCreatedResult(result));
    }

    [Fact]
    public void ControllerToCreatedResult_AcceptedKind_Throws()
    {
        var result = Result<int>.Accepted(1, Location);
        Assert.Throws<InvalidOperationException>(() => ControllerExtensions.ToCreatedResult(result));
    }

    [Fact]
    public void ControllerToCreatedResult_CreatedKind_ReturnsCreatedResult()
    {
        var mapped = ControllerExtensions.ToCreatedResult(Result<int>.Created(Location, 1));
        Assert.IsType<CreatedResult>(mapped.Result);
    }

    [Fact]
    public void ControllerToAcceptedResultOfT_OkKind_Throws()
    {
        var result = Result<int>.Ok(1);
        Assert.Throws<InvalidOperationException>(() => ControllerExtensions.ToAcceptedResult(result));
    }

    [Fact]
    public void ControllerToAcceptedResultOfT_CreatedKind_Throws()
    {
        var result = Result<int>.Created(Location, 1);
        Assert.Throws<InvalidOperationException>(() => ControllerExtensions.ToAcceptedResult(result));
    }

    [Fact]
    public void ControllerToAcceptedResultOfT_AcceptedKind_ReturnsAcceptedResult()
    {
        var mapped = ControllerExtensions.ToAcceptedResult(Result<int>.Accepted(1, Location));
        Assert.IsType<AcceptedResult>(mapped.Result);
    }

    // ════════════════════════════════════════════════════════
    // Controller surface: void Result
    // ════════════════════════════════════════════════════════

    [Fact]
    public void ControllerToNoContentResult_AcceptedKind_Throws()
    {
        var result = Result.Accepted();
        Assert.Throws<InvalidOperationException>(() => ControllerExtensions.ToNoContentResult(result));
    }

    [Fact]
    public void ControllerToNoContentResult_NoContentKind_ReturnsNoContentResult()
    {
        var mapped = ControllerExtensions.ToNoContentResult(Result.NoContent());
        Assert.IsType<NoContentResult>(mapped);
    }

    [Fact]
    public void ControllerToAcceptedResult_NoContentKind_Throws()
    {
        var result = Result.NoContent();
        Assert.Throws<InvalidOperationException>(() => ControllerExtensions.ToAcceptedResult(result));
    }

    [Fact]
    public void ControllerToAcceptedResult_AcceptedKind_ReturnsAcceptedResult()
    {
        var mapped = ControllerExtensions.ToAcceptedResult(Result.Accepted(Location));
        Assert.IsType<AcceptedResult>(mapped);
    }
}
