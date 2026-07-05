namespace MackySoft.Ucli.Tests;

using MackySoft.Tests;
using Xunit.Sdk;

public sealed class JsonPathResolverTests
{
    private static readonly InvalidJsonPathCase[] InvalidJsonPaths =
    [
        new(".status", "Unexpected '.' at index 0."),
        new("counts.", "Path must not end with '.'."),
        new("tests[0.fullName", "Expected ']'"),
        new("tests[2].fullName", "Array index 2 is out of range"),
        new("counts.missing", "Property 'missing' was not found."),
    ];

    [Fact]
    [Trait("Size", "Small")]
    public void ResolvePropertyOrThrow_ReturnsResolvedContext_ForNestedPath ()
    {
        using var document = JsonAssertionTestData.CreateSampleDocument();
        var rootContext = new JsonAssertionContext(document.RootElement, "$");

        var resolved = JsonPathResolver.ResolvePropertyOrThrow(rootContext, "counts.failed");

        Assert.Equal("$.counts.failed", resolved.Path);
        Assert.Equal(1, resolved.Value.GetInt32());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolvePropertyOrThrow_ReturnsResolvedContext_ForArrayPath ()
    {
        using var document = JsonAssertionTestData.CreateSampleDocument();
        var rootContext = new JsonAssertionContext(document.RootElement, "$");

        var resolved = JsonPathResolver.ResolvePropertyOrThrow(rootContext, "tests[1].fullName");

        Assert.Equal("$.tests[1].fullName", resolved.Path);
        Assert.Equal("Cafe.Tests.Fail", resolved.Value.GetString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolvePropertyOrThrow_Throws_WhenPathIsInvalid ()
    {
        using var document = JsonAssertionTestData.CreateSampleDocument();
        var rootContext = new JsonAssertionContext(document.RootElement, "$");

        foreach (var testCase in InvalidJsonPaths)
        {
            var exception = Assert.Throws<XunitException>(
                () => JsonPathResolver.ResolvePropertyOrThrow(rootContext, testCase.Path));

            Assert.Contains(testCase.ExpectedMessage, exception.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveIndexOrThrow_ReturnsResolvedContext_ForArrayElement ()
    {
        using var document = JsonAssertionTestData.CreateSampleDocument();
        var rootContext = new JsonAssertionContext(document.RootElement, "$");
        var arrayContext = JsonPathResolver.ResolvePropertyOrThrow(rootContext, "tests");

        var resolved = JsonPathResolver.ResolveIndexOrThrow(arrayContext, 0);

        Assert.Equal("$.tests[0]", resolved.Path);
        Assert.Equal("Cafe.Tests.Pass", resolved.Value.GetProperty("fullName").GetString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveIndexOrThrow_Throws_WhenCurrentValueIsNotArray ()
    {
        using var document = JsonAssertionTestData.CreateSampleDocument();
        var rootContext = new JsonAssertionContext(document.RootElement, "$");
        var statusContext = JsonPathResolver.ResolvePropertyOrThrow(rootContext, "status");

        var exception = Assert.Throws<XunitException>(
            () => JsonPathResolver.ResolveIndexOrThrow(statusContext, 0));

        Assert.Contains("expected array before index access", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveIndexOrThrow_Throws_WhenIndexIsNegative ()
    {
        using var document = JsonAssertionTestData.CreateSampleDocument();
        var rootContext = new JsonAssertionContext(document.RootElement, "$");
        var arrayContext = JsonPathResolver.ResolvePropertyOrThrow(rootContext, "tests");

        var exception = Assert.Throws<XunitException>(
            () => JsonPathResolver.ResolveIndexOrThrow(arrayContext, -1));

        Assert.Contains("Array index must be non-negative", exception.Message, StringComparison.Ordinal);
    }

    private sealed record InvalidJsonPathCase (
        string Path,
        string ExpectedMessage);
}
