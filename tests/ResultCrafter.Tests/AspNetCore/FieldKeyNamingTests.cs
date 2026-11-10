using System.Text.Json;
using ResultCrafter.AspNetCore;

namespace ResultCrafter.Tests.AspNetCore;

/// <summary>
///     Validation error keys must match the JSON the client sent. Each dot-separated segment is converted on
///     its own, and an index such as <c>[0]</c> is kept as is, so a snake_case policy cannot rewrite it.
/// </summary>
public sealed class FieldKeyNamingTests
{
    [Theory]
    [InlineData("camel", "Name", "name")]
    [InlineData("camel", "Address.ZipCode", "address.zipCode")]
    [InlineData("camel", "Items[0].ProductName", "items[0].productName")]
    [InlineData("camel", "Items[0]", "items[0]")]
    [InlineData("camel", "[0].Name", "[0].name")]
    [InlineData("snake", "OrderLines[12].UnitPrice", "order_lines[12].unit_price")]
    [InlineData("snake", "ShippingAddress.ZipCode", "shipping_address.zip_code")]
    [InlineData("snake", "[3]", "[3]")]
    public void ConvertPath_ConvertsEachSegment_KeepsIndexes(string policy, string path, string expected)
    {
        Assert.Equal(expected, FieldKeyNaming.ConvertPath(Policy(policy), path));
    }

    [Fact]
    public void ConvertKeys_KeysThatConvertToTheSameName_MergeTheirMessages()
    {
        var errors = new Dictionary<string, string[]>
        {
            ["Email"] = ["Required."],
            ["email"] = ["Invalid."]
        };

        var converted = FieldKeyNaming.ConvertKeys(JsonNamingPolicy.CamelCase, errors, StringComparer.Ordinal);

        var (key, messages) = Assert.Single(converted);
        Assert.Equal("email", key);
        Assert.Equal(["Required.", "Invalid."], messages);
    }

    private static JsonNamingPolicy Policy(string name)
    {
        return name switch
        {
            "camel" => JsonNamingPolicy.CamelCase,
            "snake" => JsonNamingPolicy.SnakeCaseLower,
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
        };
    }
}
