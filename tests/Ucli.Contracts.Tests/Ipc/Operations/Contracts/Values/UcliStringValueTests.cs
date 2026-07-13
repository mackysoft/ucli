using System.Reflection;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Operations.Contracts.Values;

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

    private static readonly Type[] SemanticStringValueTypes =
    [
        .. typeof(UcliStringValue).Assembly
            .GetTypes()
            .Where(type => !type.IsAbstract && type.IsSubclassOf(typeof(UcliStringValue)))
            .OrderBy(type => type.FullName, StringComparer.Ordinal),
    ];

    public static TheoryData<string> ValueTypeNames
    {
        get
        {
            var testCases = new TheoryData<string>();
            foreach (var valueType in SemanticStringValueTypes)
            {
                testCases.Add(valueType.Name);
            }

            return testCases;
        }
    }

    [Theory]
    [MemberData(nameof(ValueTypeNames))]
    [Trait("Size", "Small")]
    public void Constructor_WhenValueViolatesCommonInvariant_ThrowsArgumentException (string valueTypeName)
    {
        var valueType = GetValueType(valueTypeName);

        foreach (var invalidValue in InvalidConstructorValues)
        {
            var exception = Assert.Throws<TargetInvocationException>(
                () => Activator.CreateInstance(valueType, [invalidValue]));
            var argumentException = Assert.IsAssignableFrom<ArgumentException>(exception.InnerException);

            Assert.Equal("value", argumentException.ParamName);
        }
    }

    [Theory]
    [MemberData(nameof(ValueTypeNames))]
    [Trait("Size", "Small")]
    public void JsonDeserialize_WhenStringViolatesCommonInvariant_ThrowsJsonException (string valueTypeName)
    {
        var valueType = GetValueType(valueTypeName);

        foreach (var invalidJsonValue in InvalidJsonValues)
        {
            Assert.Throws<JsonException>(
                () => JsonSerializer.Deserialize(invalidJsonValue, valueType, IpcJsonSerializerOptions.Default));
        }
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
    public void SemanticStringValueTypes_DoNotDeclareImplicitConversions ()
    {
        var valueTypes = SemanticStringValueTypes.Prepend(typeof(UcliStringValue));

        Assert.All(
            valueTypes,
            valueType => Assert.DoesNotContain(
                valueType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly),
                method => string.Equals(method.Name, "op_Implicit", StringComparison.Ordinal)));
    }

    private static Type GetValueType (string valueTypeName)
    {
        return Assert.Single(
            SemanticStringValueTypes,
            type => string.Equals(type.Name, valueTypeName, StringComparison.Ordinal));
    }

    private sealed record TestStringValue : UcliStringValue
    {
        public TestStringValue (string value)
            : base(value)
        {
        }
    }
}
