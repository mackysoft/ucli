namespace MackySoft.Ucli.Tests;

using System.Text.Json;
using MackySoft.Tests;
using Xunit.Sdk;

public sealed class JsonAssertTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void For_CanAssertNestedPropertiesAndArrayIndices ()
    {
        using var document = JsonAssertionTestData.CreateSampleDocument();

        JsonAssert.For(document.RootElement)
            .HasString("status", "pass")
            .IsNull("errorKind")
            .HasInt32("exitCode", 0)
            .HasValueKind("counts", JsonValueKind.Object)
            .HasInt32("counts.failed", 1)
            .HasBoolean("flags.hasRetries", true)
            .HasArrayLength("tests", 2)
            .HasProperty("tests", 0, static test => test.HasString("fullName", "Cafe.Tests.Pass"))
            .HasProperty("tests", 1, static test => test.HasString("fullName", "Cafe.Tests.Fail"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void HasProperty_Throws_WhenPathCannotBeResolved ()
    {
        using var document = JsonAssertionTestData.CreateSampleDocument();

        var exception = Assert.Throws<XunitException>(
            () => JsonAssert.For(document.RootElement).HasProperty("counts.missing"));

        Assert.Contains("counts.missing", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void HasProperties_Throws_WhenNoPropertyPathIsProvided ()
    {
        using var document = JsonAssertionTestData.CreateSampleDocument();

        var exception = Assert.Throws<XunitException>(
            () => JsonAssert.For(document.RootElement).HasProperties());

        Assert.Contains("At least one property path is required.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void HasInt32_Throws_WhenValueIsNotInt32CompatibleNumber ()
    {
        using var document = JsonDocument.Parse("""{"exitCode": 1.5}""");

        var exception = Assert.Throws<XunitException>(
            () => JsonAssert.For(document.RootElement).HasInt32("exitCode", 1));

        Assert.Contains("Int32-compatible number", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void MatchesSchema_Succeeds_WhenAllRequiredNamesAndTypesMatch ()
    {
        using var document = JsonAssertionTestData.CreateSampleDocument();

        JsonAssert.For(document.RootElement).MatchesSchema(JsonAssertionTestData.CreateSampleSchema(), "sampleSchema");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void MatchesSchema_Throws_WithSchemaNameAndBulletList ()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "status": "pass"
            }
            """);

        var exception = Assert.Throws<XunitException>(
            () => JsonAssert.For(document.RootElement).MatchesSchema(JsonAssertionTestData.CreateSampleSchema(), "sampleSchema"));

        Assert.StartsWith("JSON schema validation failed. schema=sampleSchema", exception.Message, StringComparison.Ordinal);
        Assert.Contains($"{Environment.NewLine}- path '$.errorKind' is missing.", exception.Message, StringComparison.Ordinal);
    }
}