using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Json;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Values;

public sealed class UcliStringValueTests
{
    private static readonly string?[] InvalidConstructorValues =
    [
        null,
        string.Empty,
        " \t\r\n",
        " value",
        "value ",
        "\ud800",
        "\udc00",
        "\ud800A",
        "A\udc00",
    ];

    private static readonly string[] InvalidJsonValues =
    [
        "\"\"",
        "\" \\t\\r\\n\"",
        "\" value\"",
        "\"value \"",
        "\"\\uD800\"",
        "\"\\uDC00\"",
        "\"\\uD800A\"",
        "\"A\\uDC00\"",
    ];

    public static IEnumerable<object?[]> InvalidConstructorCases =>
        InvalidConstructorValues.Take(5).Select(static value => new object?[] { value });

    public static IEnumerable<object[]> InvalidJsonCases =>
        InvalidJsonValues.Select(static value => new object[] { value });

    [Theory]
    [MemberData(nameof(InvalidConstructorCases))]
    [Trait("Size", "Small")]
    public void UnityTypeIdConstructor_WhenValueViolatesCommonInvariant_ThrowsArgumentException (
        string? invalidValue)
    {
        var exception = Assert.ThrowsAny<ArgumentException>(() => new UnityTypeId(invalidValue!));

        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void UnityTypeIdConstructor_WhenValueContainsMalformedUtf16_ThrowsArgumentException ()
    {
        foreach (var invalidValue in InvalidConstructorValues.Skip(5))
        {
            var exception = Assert.ThrowsAny<ArgumentException>(() => new UnityTypeId(invalidValue!));

            Assert.Equal("value", exception.ParamName);
        }
    }

    [Theory]
    [MemberData(nameof(InvalidJsonCases))]
    [Trait("Size", "Small")]
    public void UnityTypeIdJsonDeserialize_WhenStringViolatesCommonInvariant_ThrowsJsonException (
        string invalidJsonValue)
    {
        Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<UnityTypeId>(invalidJsonValue, IpcJsonSerializerOptions.Default));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenValueContainsSurrogatePair_PreservesValue ()
    {
        const string Value = "Root/\ud83d\ude00";

        var result = new TestStringValue(Value);

        Assert.Equal(Value, result.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Equality_WhenRuntimeTypeAndValueMatch_ReturnsTrue ()
    {
        var left = new TestStringValue("value");
        var right = new TestStringValue("value");

        Assert.Equal(left, right);
        Assert.True(left == right);
        Assert.False(left != right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Equality_WhenValuesDiffer_ReturnsFalse ()
    {
        var left = new TestStringValue("left");
        var right = new TestStringValue("right");

        Assert.NotEqual(left, right);
        Assert.False(left == right);
        Assert.True(left != right);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Equality_WhenRuntimeTypesDiffer_ReturnsFalse ()
    {
        UcliStringValue left = new TestStringValue("value");
        UcliStringValue right = new OtherTestStringValue("value");

        Assert.NotEqual(left, right);
        Assert.False(left == right);
        Assert.True(left != right);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ToString_ReturnsValue ()
    {
        var value = new TestStringValue("value");

        Assert.Equal("value", value.ToString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void UnityTypeIdTryParse_WhenValueIsValid_ReturnsTypedValue ()
    {
        const string Value = "Example.Namespace.Component";

        var result = UnityTypeId.TryParse(Value, out var typeId);

        Assert.True(result);
        Assert.NotNull(typeId);
        Assert.Equal(Value, typeId.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void UnityTypeIdTryParse_WhenValueViolatesCommonInvariant_ReturnsFalseWithoutValue ()
    {
        foreach (var invalidValue in InvalidConstructorValues)
        {
            var result = UnityTypeId.TryParse(invalidValue, out var typeId);

            Assert.False(result);
            Assert.Null(typeId);
        }
    }

    private sealed class TestStringValue : UcliStringValue
    {
        public TestStringValue (string value)
            : base(value)
        {
        }
    }

    private sealed class OtherTestStringValue : UcliStringValue
    {
        public OtherTestStringValue (string value)
            : base(value)
        {
        }
    }
}
