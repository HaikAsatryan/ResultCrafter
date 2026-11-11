namespace ResultCrafter.Tests.Core;

public sealed class FieldErrorTests
{
    [Fact]
    public void Constructor_SetsMessageAndCode_ArgsEmpty()
    {
        var fieldError = new FieldError("Email is taken.", "email_in_use");

        Assert.Equal("Email is taken.", fieldError.Message);
        Assert.Equal("email_in_use", fieldError.Code);
        Assert.Empty(fieldError.Args);
    }

    [Fact]
    public void Constructor_WithoutCode_CodeIsNull()
    {
        Assert.Null(new FieldError("Email is taken.").Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_NullOrWhitespaceMessage_Throws(string? message)
    {
        Assert.ThrowsAny<ArgumentException>(() => new FieldError(message!, "code"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyOrWhitespaceCode_Throws(string code)
    {
        Assert.Throws<ArgumentException>(() => new FieldError("Message.", code));
    }

    [Fact]
    public void With_ReturnsCopy_SameKeyReplaces_OriginalUnchanged()
    {
        var original = new FieldError("Too long.", "too_long");

        var withArg = original.With("maxLength", 100);
        var replaced = withArg.With("maxLength", 50);

        Assert.Empty(original.Args);
        Assert.Equal(100, withArg.Args["maxLength"]);
        Assert.Equal(50, replaced.Args["maxLength"]);
        Assert.Equal("Too long.", replaced.Message);
        Assert.Equal("too_long", replaced.Code);
    }

    [Fact]
    public void With_StoresEveryTypedValue()
    {
        var moment = new DateTimeOffset(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);
        var id = Guid.Parse("6f9619ff-8b86-d011-b42d-00c04fc964ff");

        var fieldError = new FieldError("Message.")
            .With("s", "x")
            .With("i", 1)
            .With("l", long.MaxValue)
            .With("b", true)
            .With("d", 1.5)
            .With("m", 19.90m)
            .With("g", id)
            .With("t", moment)
            .With("u", moment.UtcDateTime)
            .With("a", ["a", "b"]);

        Assert.Equal(10, fieldError.Args.Count);
        Assert.Equal(id, fieldError.Args["g"]);
        Assert.Equal(["a", "b"], (string[])fieldError.Args["a"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void With_NullOrWhitespaceKey_Throws(string? key)
    {
        Assert.ThrowsAny<ArgumentException>(() => new FieldError("Message.").With(key!, 1));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void With_NonFiniteDouble_Throws(double value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FieldError("Message.").With("value", value));
    }

    [Fact]
    public void With_NullValues_Throw()
    {
        var fieldError = new FieldError("Message.");

        Assert.Throws<ArgumentNullException>(() => fieldError.With("s", (string)null!));
        Assert.Throws<ArgumentNullException>(() => fieldError.With("a", (string[])null!));
    }

    [Fact]
    public void With_CopiesArray()
    {
        string[] values = ["a"];

        var fieldError = new FieldError("Message.").With("a", values);
        values[0] = "changed";

        Assert.Equal("a", ((string[])fieldError.Args["a"])[0]);
    }

    [Fact]
    public void Equality_ComparesMessageCodeAndArgs()
    {
        var a = new FieldError("Too long.", "too_long").With("maxLength", 100).With("names", ["x"]);
        var b = new FieldError("Too long.", "too_long").With("maxLength", 100).With("names", ["x"]);

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, new FieldError("Too long.", "too_long").With("maxLength", 50));
        Assert.NotEqual(a, new FieldError("Too long.", "other").With("maxLength", 100).With("names", ["x"]));
        Assert.NotEqual(new FieldError("Too long."), new FieldError("Too short."));
        Assert.True(a.Equals((object)b));
    }

    [Fact]
    public void Operators_HandleNull()
    {
        FieldError? none = null;
        FieldError? alsoNone = null;

        Assert.False(new FieldError("Message.") == none);
        Assert.True(new FieldError("Message.") != none);
        Assert.True(none == alsoNone);
    }

    [Fact]
    public void ToString_IncludesCodeWhenSet()
    {
        Assert.Equal("too_long: Too long.", new FieldError("Too long.", "too_long").ToString());
        Assert.Equal("Too long.", new FieldError("Too long.").ToString());
    }
}
