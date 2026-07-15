using System.Text.Json;

namespace MackySoft.Ucli.Tests.Schemas;

public sealed class OpsDescribePayloadSchemaArtifactTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public void OpsDescribePayloadSchema_RestrictsPublicPlanModeEnum ()
    {
        var schemaPath = TestRepositoryPaths.GetFullPath(
            "schemas",
            "v1",
            "cli-output",
            "payload",
            "ops.describe.schema.json");

        var schemaText = File.ReadAllText(schemaPath);
        Assert.DoesNotContain("mayCreatePreviewState", schemaText, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(schemaText);
        var planModeEnum = document.RootElement
            .GetProperty("properties")
            .GetProperty("operation")
            .GetProperty("properties")
            .GetProperty("assurance")
            .GetProperty("properties")
            .GetProperty("planMode")
            .GetProperty("enum")
            .EnumerateArray()
            .Select(static value => value.GetString())
            .ToArray();

        Assert.Contains("validationOnly", planModeEnum);
        Assert.Contains("observesLiveUnity", planModeEnum);
        Assert.DoesNotContain("mayCreatePreviewState", planModeEnum);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void OpsDescribePayloadSchema_AcceptsVariantInputsAndClosedConstraintParameters ()
    {
        var schemaSet = CliOutputSchemaTestSupport.SchemaSet;
        using var document = JsonDocument.Parse(OpsDescribePayloadSchemaTestSupport.CreatePayload());

        var errors = schemaSet.Validate(
            "cli-output/payload/ops.describe.schema.json",
            document.RootElement);

        Assert.Empty(errors);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void OpsDescribePayloadSchema_RejectsNonPublicFreezeFields ()
    {
        var schemaSet = CliOutputSchemaTestSupport.SchemaSet;
        foreach (var testCase in OpsDescribePayloadSchemaTestSupport.GetInvalidPayloadCases())
        {
            using var document = JsonDocument.Parse(testCase.PayloadJson);

            var errors = schemaSet.Validate(
                "cli-output/payload/ops.describe.schema.json",
                document.RootElement);

            Assert.True(errors.Count > 0, testCase.Name);
        }
    }
}
