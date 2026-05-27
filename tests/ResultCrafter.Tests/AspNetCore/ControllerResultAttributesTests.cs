using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ResultCrafter.AspNetCore.Controllers;

namespace ResultCrafter.Tests.AspNetCore;

public sealed class ControllerResultAttributesTests
{
    // ════════════════════════════════════════════════════════
    // ProducesBadRequestAttribute
    // ════════════════════════════════════════════════════════

    [Fact]
    public void ProducesBadRequest_HasStatus400()
    {
        var attr = new ProducesBadRequestAttribute();
        Assert.Equal(StatusCodes.Status400BadRequest, attr.StatusCode);
    }

    [Fact]
    public void ProducesBadRequest_HasProblemDetailsType()
    {
        var attr = new ProducesBadRequestAttribute();
        Assert.Equal(typeof(ProblemDetails), attr.Type);
    }

    [Fact]
    public void ProducesBadRequest_AllowsMultipleUsages()
    {
        var usage = typeof(ProducesBadRequestAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        Assert.True(usage.AllowMultiple);
    }

    // ════════════════════════════════════════════════════════
    // ProducesUnauthorizedAttribute
    // ════════════════════════════════════════════════════════

    [Fact]
    public void ProducesUnauthorized_HasStatus401()
    {
        var attr = new ProducesUnauthorizedAttribute();
        Assert.Equal(StatusCodes.Status401Unauthorized, attr.StatusCode);
    }

    [Fact]
    public void ProducesUnauthorized_HasProblemDetailsType()
    {
        var attr = new ProducesUnauthorizedAttribute();
        Assert.Equal(typeof(ProblemDetails), attr.Type);
    }

    [Fact]
    public void ProducesUnauthorized_AllowsMultipleUsages()
    {
        var usage = typeof(ProducesUnauthorizedAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        Assert.True(usage.AllowMultiple);
    }

    // ════════════════════════════════════════════════════════
    // ProducesForbiddenAttribute
    // ════════════════════════════════════════════════════════

    [Fact]
    public void ProducesForbidden_HasStatus403()
    {
        var attr = new ProducesForbiddenAttribute();
        Assert.Equal(StatusCodes.Status403Forbidden, attr.StatusCode);
    }

    [Fact]
    public void ProducesForbidden_HasProblemDetailsType()
    {
        var attr = new ProducesForbiddenAttribute();
        Assert.Equal(typeof(ProblemDetails), attr.Type);
    }

    [Fact]
    public void ProducesForbidden_AllowsMultipleUsages()
    {
        var usage = typeof(ProducesForbiddenAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        Assert.True(usage.AllowMultiple);
    }

    // ════════════════════════════════════════════════════════
    // ProducesNotFoundAttribute
    // ════════════════════════════════════════════════════════

    [Fact]
    public void ProducesNotFound_HasStatus404()
    {
        var attr = new ProducesNotFoundAttribute();
        Assert.Equal(StatusCodes.Status404NotFound, attr.StatusCode);
    }

    [Fact]
    public void ProducesNotFound_HasProblemDetailsType()
    {
        var attr = new ProducesNotFoundAttribute();
        Assert.Equal(typeof(ProblemDetails), attr.Type);
    }

    [Fact]
    public void ProducesNotFound_AllowsMultipleUsages()
    {
        var usage = typeof(ProducesNotFoundAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        Assert.True(usage.AllowMultiple);
    }

    // ════════════════════════════════════════════════════════
    // ProducesConflictAttribute
    // ════════════════════════════════════════════════════════

    [Fact]
    public void ProducesConflict_HasStatus409()
    {
        var attr = new ProducesConflictAttribute();
        Assert.Equal(StatusCodes.Status409Conflict, attr.StatusCode);
    }

    [Fact]
    public void ProducesConflict_HasProblemDetailsType()
    {
        var attr = new ProducesConflictAttribute();
        Assert.Equal(typeof(ProblemDetails), attr.Type);
    }

    [Fact]
    public void ProducesConflict_AllowsMultipleUsages()
    {
        var usage = typeof(ProducesConflictAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        Assert.True(usage.AllowMultiple);
    }

    // ════════════════════════════════════════════════════════
    // All attributes inherit from ProducesResponseTypeAttribute
    // ════════════════════════════════════════════════════════

    [Theory]
    [InlineData(typeof(ProducesBadRequestAttribute))]
    [InlineData(typeof(ProducesUnauthorizedAttribute))]
    [InlineData(typeof(ProducesForbiddenAttribute))]
    [InlineData(typeof(ProducesNotFoundAttribute))]
    [InlineData(typeof(ProducesConflictAttribute))]
    public void AllAttributes_InheritFromProducesResponseTypeAttribute(Type attributeType)
    {
        Assert.True(
            typeof(ProducesResponseTypeAttribute).IsAssignableFrom(attributeType),
            $"{attributeType.Name} should inherit from ProducesResponseTypeAttribute");
    }

    // ════════════════════════════════════════════════════════
    // All attributes target methods only
    // ════════════════════════════════════════════════════════

    [Theory]
    [InlineData(typeof(ProducesBadRequestAttribute))]
    [InlineData(typeof(ProducesUnauthorizedAttribute))]
    [InlineData(typeof(ProducesForbiddenAttribute))]
    [InlineData(typeof(ProducesNotFoundAttribute))]
    [InlineData(typeof(ProducesConflictAttribute))]
    public void AllAttributes_TargetMethods(Type attributeType)
    {
        var usage = attributeType
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        Assert.True(
            (usage.ValidOn & AttributeTargets.Method) != 0,
            $"{attributeType.Name} should target AttributeTargets.Method");
    }
}
