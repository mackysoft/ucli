namespace MackySoft.Ucli.Tests;

using System.Text.Json;
using MackySoft.Tests;

public sealed class JsonSchemaValidatorTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Validate_ReturnsNoError_WhenSchemaMatches ()
    {
        using var document = JsonAssertionTestData.CreateSampleDocument();

        var errors = JsonSchemaValidator.Validate(document.RootElement, JsonAssertionTestData.CreateSampleSchema(), "$");

        Assert.Empty(errors);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_ReturnsMissingPropertyError_WhenRequiredPropertyIsMissing ()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "status": "pass"
            }
            """);
        var errors = JsonSchemaValidator.Validate(document.RootElement, JsonAssertionTestData.CreateSampleSchema(), "$");

        Assert.Contains("path '$.errorKind' is missing.", errors, StringComparer.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_ReturnsTypeError_WhenValueTypeDoesNotMatch ()
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
        var errors = JsonSchemaValidator.Validate(document.RootElement, JsonAssertionTestData.CreateSampleSchema(), "$");

        Assert.Contains("path '$.exitCode' expected one of [Int32] but was Number(non-Int32).", errors, StringComparer.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_ReturnsArrayItemTypeError_WhenArrayItemTypeDoesNotMatch ()
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
        var errors = JsonSchemaValidator.Validate(document.RootElement, JsonAssertionTestData.CreateSampleSchema(), "$");

        Assert.Contains(
            "path '$.tests[1].fullName' expected one of [String] but was Number.",
            errors,
            StringComparer.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_ReturnsAdditionalPropertyError_WhenAdditionalPropertiesAreDisallowed ()
    {
        using var document = JsonAssertionTestData.CreateSampleDocument();
        var schema = JsonSchemaNode.Object(
            static root => root.Required("status", JsonSchemaNode.Value(JsonSchemaType.String)),
            allowAdditionalProperties: false);
        var errors = JsonSchemaValidator.Validate(document.RootElement, schema, "$");

        Assert.Contains("path '$.errorKind' is not allowed by schema.", errors, StringComparer.Ordinal);
    }
}