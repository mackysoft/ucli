namespace MackySoft.Ucli.Tests;

using System.Text.Json;
using MackySoft.Tests;
using Xunit.Sdk;

public sealed class JsonGoldenFileAssertTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void MatchesFile_WithEquivalentJson_Succeeds ()
    {
        using var scope = TestDirectories.CreateTempScope("json-golden-file-assert", "equivalent");
        var goldenPath = scope.WriteFile(
            "expected.json",
            """
            {
              "value": true
            }
            """);

        JsonGoldenFileAssert.MatchesFile(
            goldenPath,
            """
            {
              "value": true
            }

            """);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void MatchesFile_WithInvalidGoldenJson_Throws ()
    {
        using var scope = TestDirectories.CreateTempScope("json-golden-file-assert", "invalid-golden");
        var goldenPath = scope.WriteFile("expected.json", """{"value":""");

        var exception = Assert.Throws<XunitException>(
            () => JsonGoldenFileAssert.MatchesFile(
                goldenPath,
                """
                {
                  "value": true
                }

                """));

        Assert.Contains("Golden JSON is invalid", exception.Message, StringComparison.Ordinal);
        Assert.Contains(goldenPath, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void MatchesFile_WithSingleLineActual_Throws ()
    {
        using var scope = TestDirectories.CreateTempScope("json-golden-file-assert", "single-line-actual");
        var goldenPath = scope.WriteFile(
            "expected.json",
            """
            {
              "value": true
            }
            """);

        var exception = Assert.ThrowsAny<XunitException>(
            () => JsonGoldenFileAssert.MatchesFile(goldenPath, """{"value":true}""" + Environment.NewLine));

        Assert.Contains("pretty-printed", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void MatchesFile_WithMultipleActualJsonObjects_Throws ()
    {
        using var scope = TestDirectories.CreateTempScope("json-golden-file-assert", "multiple-actual");
        var goldenPath = scope.WriteFile(
            "expected.json",
            """
            {
              "value": true
            }
            """);

        var exception = Assert.Throws<XunitException>(
            () => JsonGoldenFileAssert.MatchesFile(
                goldenPath,
                """
                {
                  "value": true
                }
                {
                  "value": false
                }

                """));

        Assert.Contains("single pretty-printed object", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void MatchesFile_WithRequestIdNormalization_ReplacesGuid ()
    {
        using var scope = TestDirectories.CreateTempScope("json-golden-file-assert", "request-id");
        var goldenPath = scope.WriteFile(
            "expected.json",
            """
            {
              "payload": {
                "requestId": "<requestId>"
              }
            }
            """);

        JsonGoldenFileAssert.MatchesFile(
            goldenPath,
            """
            {
              "payload": {
                "requestId": "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62"
              }
            }

            """,
            JsonGoldenFileNormalization.Create().NormalizeRequestIds());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void MatchesFile_WithInvalidRequestId_Throws ()
    {
        using var scope = TestDirectories.CreateTempScope("json-golden-file-assert", "invalid-request-id");
        var goldenPath = scope.WriteFile(
            "expected.json",
            """
            {
              "payload": {
                "requestId": "<requestId>"
              }
            }
            """);

        var exception = Assert.Throws<XunitException>(
            () => JsonGoldenFileAssert.MatchesFile(
                goldenPath,
                """
                {
                  "payload": {
                    "requestId": "not-a-guid"
                  }
                }

                """,
                JsonGoldenFileNormalization.Create().NormalizeRequestIds()));

        Assert.Contains("requestId", exception.Message, StringComparison.Ordinal);
        Assert.Contains("GUID", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void MatchesFile_WithPathPrefixNormalization_UsesTokenizedSlashPath ()
    {
        using var scope = TestDirectories.CreateTempScope("json-golden-file-assert", "path-prefix");
        var workspacePath = scope.CreateDirectory("workspace");
        var actualPath = Path.Combine(workspacePath, "UnityProject", ".ucli", "config.json");
        var actualPathLiteral = JsonSerializer.Serialize(actualPath);
        var goldenPath = scope.WriteFile(
            "expected.json",
            """
            {
              "path": "<workspace>/UnityProject/.ucli/config.json"
            }
            """);

        JsonGoldenFileAssert.MatchesFile(
            goldenPath,
            $$"""
            {
              "path": {{actualPathLiteral}}
            }

            """,
            JsonGoldenFileNormalization.Create().NormalizePathPrefix(workspacePath, "<workspace>"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void MatchesFile_WithStringPropertyNormalization_ValidatesAndReplacesTimestamp ()
    {
        using var scope = TestDirectories.CreateTempScope("json-golden-file-assert", "timestamp");
        var goldenPath = scope.WriteFile(
            "expected.json",
            """
            {
              "generatedAtUtc": "<timestamp>"
            }
            """);

        JsonGoldenFileAssert.MatchesFile(
            goldenPath,
            """
            {
              "generatedAtUtc": "2026-03-06T00:00:00+00:00"
            }

            """,
            JsonGoldenFileNormalization.Create().NormalizeStringProperty(
                "generatedAtUtc",
                "<timestamp>",
                static value => DateTimeOffset.TryParse(value, out _),
                "an ISO-8601 timestamp"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void MatchesFile_WithMismatch_ReportsLineDiff ()
    {
        using var scope = TestDirectories.CreateTempScope("json-golden-file-assert", "mismatch");
        var goldenPath = scope.WriteFile(
            "expected.json",
            """
            {
              "value": 1
            }
            """);

        var exception = Assert.Throws<XunitException>(
            () => JsonGoldenFileAssert.MatchesFile(
                goldenPath,
                """
                {
                  "value": 2
                }

                """));

        Assert.Contains("Golden JSON mismatch", exception.Message, StringComparison.Ordinal);
        Assert.Contains("@@ line 2 @@", exception.Message, StringComparison.Ordinal);
        Assert.Contains("-   \"value\": 1", exception.Message, StringComparison.Ordinal);
        Assert.Contains("+   \"value\": 2", exception.Message, StringComparison.Ordinal);
    }
}
