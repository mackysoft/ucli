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
        using var document = CreateSampleDocument();
        var assertion = JsonAssert.For(document.RootElement);

        assertion
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
        using var document = CreateSampleDocument();

        var exception = Assert.Throws<XunitException>(
            () => JsonAssert.For(document.RootElement).HasProperty("counts.missing"));

        Assert.Contains("counts.missing", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void HasString_Throws_WhenPathSyntaxIsInvalid ()
    {
        using var document = CreateSampleDocument();

        var exception = Assert.Throws<XunitException>(
            () => JsonAssert.For(document.RootElement).HasString("tests[].fullName", "Cafe.Tests.Pass"));

        Assert.Contains("Array index was not specified", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void HasString_Throws_WhenPathIndexIsOutOfRange ()
    {
        using var document = CreateSampleDocument();

        var exception = Assert.Throws<XunitException>(
            () => JsonAssert.For(document.RootElement).HasString("tests[2].fullName", "Cafe.Tests.Unknown"));

        Assert.Contains("tests[2]", exception.Message, StringComparison.Ordinal);
        Assert.Contains("out of range", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void HasProperty_WithIndex_Throws_WhenArrayIndexIsOutOfRange ()
    {
        using var document = CreateSampleDocument();

        var exception = Assert.Throws<XunitException>(
            () => JsonAssert.For(document.RootElement)
                .HasProperty("tests", 2, static test => test.HasString("fullName", "Cafe.Tests.Unknown")));

        Assert.Contains("$.tests", exception.Message, StringComparison.Ordinal);
        Assert.Contains("out of range", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void MatchesSchema_Succeeds_WhenAllRequiredNamesAndTypesMatch ()
    {
        using var document = CreateSampleDocument();

        JsonAssert.For(document.RootElement).MatchesSchema(CreateSampleSchema(), "sampleSchema");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void MatchesSchema_Throws_WhenRequiredPropertyIsMissing ()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "status": "pass",
              "exitCode": 0,
              "counts": {
                "passed": 10,
                "failed": 1
              },
              "flags": {
                "hasRetries": true
              },
              "tests": [
                {
                  "fullName": "Cafe.Tests.Pass"
                }
              ]
            }
            """);

        var exception = Assert.Throws<XunitException>(
            () => JsonAssert.For(document.RootElement).MatchesSchema(CreateSampleSchema(), "sampleSchema"));

        Assert.Contains("$.errorKind", exception.Message, StringComparison.Ordinal);
        Assert.Contains("is missing", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void MatchesSchema_Throws_WhenPropertyTypeDoesNotMatch ()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "status": "pass",
              "errorKind": null,
              "exitCode": 1.5,
              "counts": {
                "passed": 10,
                "failed": 1
              },
              "flags": {
                "hasRetries": true
              },
              "tests": [
                {
                  "fullName": "Cafe.Tests.Pass"
                }
              ]
            }
            """);

        var exception = Assert.Throws<XunitException>(
            () => JsonAssert.For(document.RootElement).MatchesSchema(CreateSampleSchema(), "sampleSchema"));

        Assert.Contains("$.exitCode", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Int32", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void MatchesSchema_Throws_WhenArrayItemTypeDoesNotMatch ()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "status": "pass",
              "errorKind": null,
              "exitCode": 0,
              "counts": {
                "passed": 10,
                "failed": 1
              },
              "flags": {
                "hasRetries": true
              },
              "tests": [
                {
                  "fullName": "Cafe.Tests.Pass"
                },
                {
                  "fullName": 42
                }
              ]
            }
            """);

        var exception = Assert.Throws<XunitException>(
            () => JsonAssert.For(document.RootElement).MatchesSchema(CreateSampleSchema(), "sampleSchema"));

        Assert.Contains("$.tests[1].fullName", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void MatchesSchema_AllowsAdditionalProperties_ByDefault ()
    {
        using var document = CreateSampleDocument();
        var schema = JsonSchemaNode.Object(
            static root => root.Required("status", JsonSchemaNode.Value(JsonSchemaType.String)));

        JsonAssert.For(document.RootElement).MatchesSchema(schema, "statusOnly");
    }

    private static JsonDocument CreateSampleDocument ()
    {
        return JsonDocument.Parse(
            """
            {
              "status": "pass",
              "errorKind": null,
              "exitCode": 0,
              "counts": {
                "passed": 10,
                "failed": 1
              },
              "flags": {
                "hasRetries": true
              },
              "tests": [
                {
                  "fullName": "Cafe.Tests.Pass"
                },
                {
                  "fullName": "Cafe.Tests.Fail"
                }
              ]
            }
            """);
    }

    private static JsonSchemaNode CreateSampleSchema ()
    {
        return JsonSchemaNode.Object(
            static root => root
                .Required("status", JsonSchemaNode.Value(JsonSchemaType.String))
                .Required("errorKind", JsonSchemaNode.Union(JsonSchemaType.String, JsonSchemaType.Null))
                .Required("exitCode", JsonSchemaNode.Value(JsonSchemaType.Int32))
                .RequiredObject(
                    "counts",
                    static counts => counts
                        .Required("passed", JsonSchemaNode.Value(JsonSchemaType.Int32))
                        .Required("failed", JsonSchemaNode.Value(JsonSchemaType.Int32)))
                .RequiredObject(
                    "flags",
                    static flags => flags
                        .Required("hasRetries", JsonSchemaNode.Value(JsonSchemaType.Boolean)))
                .RequiredArrayOfObject(
                    "tests",
                    static tests => tests
                        .Required("fullName", JsonSchemaNode.Value(JsonSchemaType.String))));
    }
}