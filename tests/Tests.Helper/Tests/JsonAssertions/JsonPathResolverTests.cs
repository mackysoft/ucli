namespace MackySoft.Ucli.Tests;

using MackySoft.Tests;
using Xunit.Sdk;

public sealed class JsonPathResolverTests
{
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

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(".status", "Unexpected '.' at index 0.")]
    [InlineData("counts.", "Path must not end with '.'.")]
    [InlineData("tests[0.fullName", "Expected ']'")]
    [InlineData("tests[2].fullName", "Array index 2 is out of range")]
    [InlineData("counts.missing", "Property 'missing' was not found.")]
    public void ResolvePropertyOrThrow_Throws_WhenPathIsInvalid (
        string path,
        string expectedMessage)
    {
        using var document = JsonAssertionTestData.CreateSampleDocument();
        var rootContext = new JsonAssertionContext(document.RootElement, "$");

        var exception = Assert.Throws<XunitException>(
            () => JsonPathResolver.ResolvePropertyOrThrow(rootContext, path));

        Assert.Contains(expectedMessage, exception.Message, StringComparison.Ordinal);
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
}