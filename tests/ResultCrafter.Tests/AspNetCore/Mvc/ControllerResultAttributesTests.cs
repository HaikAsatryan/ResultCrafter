using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Formatters;
using ResultCrafter.AspNetCore.Mvc;
using ResultCrafter.AspNetCore.ProblemDetails;

namespace ResultCrafter.Tests.AspNetCore.Mvc;

public sealed class ControllerResultAttributesTests
{
    [Theory]
    [InlineData(typeof(ProducesBadRequestAttribute), StatusCodes.Status400BadRequest)]
    [InlineData(typeof(ProducesUnauthorizedAttribute), StatusCodes.Status401Unauthorized)]
    [InlineData(typeof(ProducesForbiddenAttribute), StatusCodes.Status403Forbidden)]
    [InlineData(typeof(ProducesNotFoundAttribute), StatusCodes.Status404NotFound)]
    [InlineData(typeof(ProducesConflictAttribute), StatusCodes.Status409Conflict)]
    [InlineData(typeof(ProducesGoneAttribute), StatusCodes.Status410Gone)]
    [InlineData(typeof(ProducesUnprocessableEntityAttribute), StatusCodes.Status422UnprocessableEntity)]
    [InlineData(typeof(ProducesTooManyRequestsAttribute), StatusCodes.Status429TooManyRequests)]
    public void Attribute_DeclaresProblemResponseForItsStatus(Type attributeType, int expectedStatus)
    {
        var attr = (ProducesResponseTypeAttribute)Activator.CreateInstance(attributeType)!;
        var usage = attributeType
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        Assert.Equal(expectedStatus, attr.StatusCode);
        Assert.Equal(typeof(ProblemResponse), attr.Type);
        var contentTypes = new MediaTypeCollection();
        ((IApiResponseMetadataProvider)attr).SetContentTypes(contentTypes);
        Assert.Equal("application/problem+json", Assert.Single(contentTypes));
        Assert.True(usage.AllowMultiple);
        Assert.Equal(AttributeTargets.Method, usage.ValidOn);
    }
}
