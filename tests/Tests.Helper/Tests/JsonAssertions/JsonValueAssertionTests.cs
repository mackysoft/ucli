namespace MackySoft.Ucli.Tests;

using System.Text.Json;
using MackySoft.Tests;
using Xunit.Sdk;

public sealed class JsonValueAssertionTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void AssertString_Succeeds_WhenActualMatchesExpected ()
    {
        using var document = JsonAssertionTestData.CreateSampleDocument();
        var context = new JsonAssertionContext(document.RootElement.GetProperty("status"), "$.status");

        JsonValueAssertion.AssertString(context, "pass");
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("String", "counts", "$.counts", "expected string but was 'Object'")]
    [InlineData("Boolean", "status", "$.status", "expected boolean but was 'String'")]
    [InlineData("Null", "status", "$.status", "expected null but was 'String'")]
    [InlineData("ArrayLength", "status", "$.status", "expected array but was 'String'")]
    [InlineData("ValueKind", "tests", "$.tests", "expected kind 'Object' but was 'Array'")]
    public void Assertion_Throws_WhenActualTypeDoesNotMatch (
        string assertionType,
        string propertyName,
        string path,
        string expectedMessage)
    {
        using var document = JsonAssertionTestData.CreateSampleDocument();
        var context = new JsonAssertionContext(document.RootElement.GetProperty(propertyName), path);
        XunitException exception;

        switch (assertionType)
        {
            case "String":
                exception = Assert.Throws<XunitException>(() => JsonValueAssertion.AssertString(context, "x"));
                break;
            case "Boolean":
                exception = Assert.Throws<XunitException>(() => JsonValueAssertion.AssertBoolean(context, true));
                break;
            case "Null":
                exception = Assert.Throws<XunitException>(() => JsonValueAssertion.AssertNull(context));
                break;
            case "ArrayLength":
                exception = Assert.Throws<XunitException>(() => JsonValueAssertion.AssertArrayLength(context, 1));
                break;
            case "ValueKind":
                exception = Assert.Throws<XunitException>(() => JsonValueAssertion.AssertValueKind(context, JsonValueKind.Object));
                break;
            default:
                throw new InvalidOperationException($"Unsupported assertion type '{assertionType}'.");
        }

        Assert.Contains(expectedMessage, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void AssertInt32_Throws_WhenActualNumberIsNotInt32Compatible ()
    {
        using var document = JsonDocument.Parse("""{"exitCode": 1.5}""");
        var context = new JsonAssertionContext(document.RootElement.GetProperty("exitCode"), "$.exitCode");

        var exception = Assert.Throws<XunitException>(() => JsonValueAssertion.AssertInt32(context, 1));

        Assert.Contains("Int32-compatible number", exception.Message, StringComparison.Ordinal);
    }

}
