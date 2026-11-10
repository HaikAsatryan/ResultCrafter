using System.Data.Common;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using ResultCrafter.AspNetCore;
using ResultCrafter.AspNetCore.EfCore;

namespace ResultCrafter.Tests.AspNetCore.EfCore;

/// <summary>
///     A real optimistic-concurrency conflict is a 409. Npgsql EF 11 also throws
///     <see cref="DbUpdateConcurrencyException" /> for a serialization failure (40001) and a deadlock (40P01);
///     those are server-side failures and must reach the 500 handler instead.
/// </summary>
public sealed class EfCoreConcurrencyHandlerTests
{
    [Theory]
    [InlineData("none", HttpStatusCode.Conflict, "concurrency_conflict")]
    [InlineData("40001", HttpStatusCode.InternalServerError, "internal_server_error")]
    [InlineData("40P01 nested", HttpStatusCode.InternalServerError, "internal_server_error")]
    public async Task DbUpdateConcurrencyException_MapsByInnerSqlState(string inner,
        HttpStatusCode expectedStatus,
        string expectedTitle)
    {
        Exception? innerException = inner switch
        {
            "none" => null,
            "40001" => new FakeDbException("40001"),
            "40P01 nested" => new InvalidOperationException("wrapper", new FakeDbException("40P01")),
            _ => throw new ArgumentOutOfRangeException(nameof(inner))
        };

        await using var app = TestApp.Build(
            b => b.Services.AddResultCrafter().AddResultCrafterEfCore(),
            a =>
            {
                a.UseResultCrafter();
                a.MapGet("/save",
                    IResult () => throw new DbUpdateConcurrencyException("save failed", innerException));
            });

        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/save", TestContext.Current.CancellationToken);

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var doc = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal((int)expectedStatus, doc.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(expectedTitle, doc.RootElement.GetProperty("title").GetString());
        Assert.False(doc.RootElement.TryGetProperty("x-rc", out _));
        Assert.False(doc.RootElement.TryGetProperty("x-rc-error-id", out _));
    }

    // Stands in for a provider exception such as Npgsql's PostgresException without referencing a provider.
    private sealed class FakeDbException(string sqlState) : DbException("provider error")
    {
        public override string SqlState => sqlState;
    }
}
