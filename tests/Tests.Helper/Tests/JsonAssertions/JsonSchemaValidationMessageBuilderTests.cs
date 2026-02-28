namespace MackySoft.Ucli.Tests;

using MackySoft.Tests;

public sealed class JsonSchemaValidationMessageBuilderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Build_UsesSchemaNamePrefixAndBulletList ()
    {
        var errors = new[]
        {
            "path '$.errorKind' is missing.",
            "path '$.exitCode' expected one of [Int32] but was Number(non-Int32).",
        };

        var message = JsonSchemaValidationMessageBuilder.Build(errors, "sampleSchema");

        Assert.StartsWith("JSON schema validation failed. schema=sampleSchema", message, StringComparison.Ordinal);
        Assert.Contains($"{Environment.NewLine}- path '$.errorKind' is missing.", message, StringComparison.Ordinal);
        Assert.Contains(
            $"{Environment.NewLine}- path '$.exitCode' expected one of [Int32] but was Number(non-Int32).",
            message,
            StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Build_UsesDefaultPrefix_WhenSchemaNameIsNull ()
    {
        var errors = new[]
        {
            "path '$.status' expected one of [String] but was Null.",
        };

        var message = JsonSchemaValidationMessageBuilder.Build(errors, null);

        Assert.StartsWith("JSON schema validation failed.", message, StringComparison.Ordinal);
        Assert.Contains($"{Environment.NewLine}- path '$.status' expected one of [String] but was Null.", message, StringComparison.Ordinal);
    }
}