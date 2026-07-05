namespace MackySoft.Ucli.Tests;

using System.Text.Json;
using MackySoft.Tests;
using Xunit.Sdk;

public sealed class JsonValueAssertionTests
{
    private static readonly ValueAssertionTypeMismatchCase[] TypeMismatchCases =
    [
        new(
            PropertyName: "counts",
            Path: "$.counts",
            ExpectedMessage: "expected string but was 'Object'",
            Assertion: context => JsonValueAssertion.AssertString(context, "x")),
        new(
            PropertyName: "status",
            Path: "$.status",
            ExpectedMessage: "expected boolean but was 'String'",
            Assertion: context => JsonValueAssertion.AssertBoolean(context, true)),
        new(
            PropertyName: "status",
            Path: "$.status",
            ExpectedMessage: "expected null but was 'String'",
            Assertion: JsonValueAssertion.AssertNull),
        new(
            PropertyName: "status",
            Path: "$.status",
            ExpectedMessage: "expected array but was 'String'",
            Assertion: context => JsonValueAssertion.AssertArrayLength(context, 1)),
        new(
            PropertyName: "tests",
            Path: "$.tests",
            ExpectedMessage: "expected kind 'Object' but was 'Array'",
            Assertion: context => JsonValueAssertion.AssertValueKind(context, JsonValueKind.Object)),
    ];

    [Fact]
    [Trait("Size", "Small")]
    public void AssertString_Succeeds_WhenActualMatchesExpected ()
    {
        using var document = JsonAssertionTestData.CreateSampleDocument();
        var context = new JsonAssertionContext(document.RootElement.GetProperty("status"), "$.status");

        JsonValueAssertion.AssertString(context, "pass");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Assertion_Throws_WhenActualTypeDoesNotMatch ()
    {
        using var document = JsonAssertionTestData.CreateSampleDocument();

        foreach (var testCase in TypeMismatchCases)
        {
            var context = new JsonAssertionContext(document.RootElement.GetProperty(testCase.PropertyName), testCase.Path);

            var exception = Assert.Throws<XunitException>(() => testCase.Assertion(context));

            Assert.Contains(testCase.ExpectedMessage, exception.Message, StringComparison.Ordinal);
        }
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

    private sealed record ValueAssertionTypeMismatchCase (
        string PropertyName,
        string Path,
        string ExpectedMessage,
        Action<JsonAssertionContext> Assertion);
}
