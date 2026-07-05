using System.Text.Json;

namespace MackySoft.Ucli.Tests.Schemas;

public sealed class ReportRefSchemaArtifactTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public void ReportRefSchema_RequiresExactlyOneLocation ()
    {
        var schemaSet = CliOutputSchemaTestSupport.SchemaSet;
        foreach (ReportRefContractCase testCase in GetReportRefContractCases())
        {
            using var document = JsonDocument.Parse(testCase.Json);

            var errors = schemaSet.Validate("cli-output/defs/report-ref.schema.json", document.RootElement);

            if (testCase.ExpectedValid)
            {
                Assert.True(errors.Count == 0, $"{testCase.Name}: {string.Join(Environment.NewLine, errors)}");
            }
            else
            {
                Assert.True(errors.Count > 0, $"{testCase.Name} should be rejected.");
            }
        }
    }

    private static ReportRefContractCase[] GetReportRefContractCases ()
    {
        return
        [
            new(
                "artifact path with digest",
                """
                {
                  "path": "artifacts/ready.log",
                  "digest": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
                }
                """,
                ExpectedValid: true),
            new(
                "external uri",
                """
                {
                  "uri": "https://example.test/report"
                }
                """,
                ExpectedValid: true),
            new(
                "digest without location",
                """
                {
                  "digest": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
                }
                """,
                ExpectedValid: false),
            new(
                "path and uri",
                """
                {
                  "path": "artifacts/ready.log",
                  "uri": "https://example.test/report"
                }
                """,
                ExpectedValid: false),
        ];
    }

    private readonly record struct ReportRefContractCase (
        string Name,
        string Json,
        bool ExpectedValid);
}
