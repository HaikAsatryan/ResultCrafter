using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using ResultCrafter;
using ResultCrafter.AspNetCore;
using ResultCrafter.AspNetCore.FluentValidation;

// NativeAOT smoke test: the app calls its own endpoints and checks status codes and key JSON members.
// Reflection-based JSON is off in a NativeAOT app, which is what this proves the pipeline survives.
var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { Args = args, EnvironmentName = "Production" });
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Logging.ClearProviders();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.TypeInfoResolverChain.Insert(0, SmokeJsonContext.Default));
builder.Services.AddResultCrafter();
builder.Services.AddScoped<IValidator<CreateItem>, CreateItemValidator>();

var app = builder.Build();
app.UseResultCrafter();

app.MapGet("/ok", () => Result<Item>.Ok(new Item(1, "a")).ToOkResult());
app.MapPost("/created", () => Result<Item>.Created("/items/1", new Item(1, "a")).ToCreatedResult());
app.MapGet("/not-found", () => Result<Item>.Fail(Error.NotFound("item_missing")).ToOkResult());
app.MapGet("/bad-request", () =>
    Error.BadRequest(new Dictionary<string, string[]> { ["name"] = ["required"] }).ToProblemResult());
app.MapPost("/validated", (CreateItem item) => TypedResults.Ok(item.Name)).WithValidation<CreateItem>();
app.MapGet("/limited", () => Error.TooManyRequests("slow_down", TimeSpan.FromSeconds(59.2))
    .With("s", "x")
    .With("i", 1)
    .With("l", long.MaxValue)
    .With("b", true)
    .With("d", 1.5)
    .With("m", 19.90m)
    .With("g", Guid.Parse("6f9619ff-8b86-d011-b42d-00c04fc964ff"))
    .With("t", new DateTimeOffset(2026, 11, 10, 12, 30, 0, TimeSpan.Zero))
    .With("u", new DateTime(2026, 11, 10, 12, 30, 0, DateTimeKind.Utc))
    .With("a", ["a", "b"])
    .ToProblemResult());
app.MapGet("/throw", IResult () => throw new InvalidOperationException("boom"));
app.MapPost("/body", (CreateItem item) => TypedResults.Ok(item.Name));

await app.StartAsync();
using var http = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };
var failures = new List<string>();

await Check("Ok", HttpMethod.Get, "/ok", null, HttpStatusCode.OK, (response, root) =>
    root.GetProperty("name").GetString() == "a");

await Check("Created", HttpMethod.Post, "/created", null, HttpStatusCode.Created, (response, root) =>
    response.Headers.Location?.ToString() == "/items/1" && root.GetProperty("id").GetInt32() == 1);

await Check("NotFound", HttpMethod.Get, "/not-found", null, HttpStatusCode.NotFound, (response, root) =>
    root.GetProperty("title").GetString() == "not_found" &&
    root.GetProperty("detail").GetString() == "item_missing" &&
    !root.TryGetProperty("x-rc", out _));

await Check("BadRequest field errors", HttpMethod.Get, "/bad-request", null, HttpStatusCode.BadRequest, (response, root) =>
    root.GetProperty("errors").GetProperty("name")[0].GetString() == "required");

await Check("WithValidation invalid body", HttpMethod.Post, "/validated", """{"name":""}""", HttpStatusCode.BadRequest,
    (response, root) => root.GetProperty("errors").TryGetProperty("name", out _));

await Check("429 Retry-After and With()", HttpMethod.Get, "/limited", null, HttpStatusCode.TooManyRequests,
    (response, root) =>
        response.Headers.RetryAfter?.ToString() == "60" &&
        root.GetProperty("s").GetString() == "x" &&
        root.GetProperty("i").GetInt32() == 1 &&
        root.GetProperty("l").GetInt64() == long.MaxValue &&
        root.GetProperty("b").GetBoolean() &&
        root.GetProperty("d").GetDouble() == 1.5 &&
        root.GetProperty("m").GetRawText() == "19.90" &&
        root.GetProperty("g").GetString() == "6f9619ff-8b86-d011-b42d-00c04fc964ff" &&
        root.GetProperty("t").GetString() == "2026-11-10T12:30:00+00:00" &&
        root.GetProperty("u").GetString() == "2026-11-10T12:30:00Z" &&
        root.GetProperty("a")[1].GetString() == "b" &&
        !root.TryGetProperty("x-rc-retry-after", out _));

await Check("Unhandled exception", HttpMethod.Get, "/throw", null, HttpStatusCode.InternalServerError, (response, root) =>
    root.GetProperty("title").GetString() == "internal_server_error");

await Check("Unmatched route", HttpMethod.Get, "/nope", null, HttpStatusCode.NotFound, (response, root) =>
    root.GetProperty("title").GetString() == "not_found");

// .NET 11 reports which field failed; .NET 10 does not, so the body stays generic there.
await Check("JSON body error", HttpMethod.Post, "/body", """{"name":"x","count":"abc"}""", HttpStatusCode.BadRequest,
    (response, root) => root.GetProperty("title").GetString() == "bad_request" && (Environment.Version.Major >= 11
        ? root.GetProperty("errors").GetProperty("count")[0].GetString() == "invalid_value"
        : !root.TryGetProperty("errors", out _)));

await app.StopAsync();

Console.WriteLine(failures.Count == 0
    ? $"AOT smoke passed on .NET {Environment.Version}."
    : $"AOT smoke FAILED on .NET {Environment.Version}: {string.Join(", ", failures)}");
return failures.Count == 0 ? 0 : 1;

async Task Check(string name, HttpMethod method, string path, string? body, HttpStatusCode expectedStatus,
    Func<HttpResponseMessage, JsonElement, bool> assert)
{
    using var request = new HttpRequestMessage(method, path);
    if (body is not null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");

    using var response = await http.SendAsync(request);
    var text = await response.Content.ReadAsStringAsync();

    try
    {
        using var doc = JsonDocument.Parse(text);
        if (response.StatusCode == expectedStatus && assert(response, doc.RootElement))
        {
            Console.WriteLine($"  ok   {name}");
            return;
        }
    }
    catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
    {
        // Falls through to the failure report below with the raw body.
    }

    failures.Add(name);
    Console.WriteLine($"  FAIL {name}: {(int)response.StatusCode} {text}");
}

internal sealed record Item(int Id, string Name);

internal sealed record CreateItem(string? Name, int Count);

internal sealed class CreateItemValidator : AbstractValidator<CreateItem>
{
    public CreateItemValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
    }
}

[JsonSerializable(typeof(Item))]
[JsonSerializable(typeof(CreateItem))]
[JsonSerializable(typeof(string))]
internal sealed partial class SmokeJsonContext : JsonSerializerContext;
