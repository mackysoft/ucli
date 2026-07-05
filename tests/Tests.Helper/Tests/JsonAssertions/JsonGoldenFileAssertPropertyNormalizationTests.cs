namespace MackySoft.Ucli.Tests;

using MackySoft.Tests;
using Xunit.Sdk;

public sealed class JsonGoldenFileAssertPropertyNormalizationTests
{
    [Fact]
    [Trait("Size", "Medium")]
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
            new JsonGoldenFileNormalization().NormalizeGuidProperty("requestId", "<requestId>"));
    }

    [Fact]
    [Trait("Size", "Medium")]
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
                new JsonGoldenFileNormalization().NormalizeGuidProperty("requestId", "<requestId>")));

        Assert.Contains("requestId", exception.Message, StringComparison.Ordinal);
        Assert.Contains("GUID", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void MatchesFile_WithTimestampPropertyNormalization_ValidatesAndReplacesTimestamp ()
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
            new JsonGoldenFileNormalization().NormalizeTimestampProperty("generatedAtUtc"));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void MatchesFile_WithNonIsoTimestamp_Throws ()
    {
        using var scope = TestDirectories.CreateTempScope("json-golden-file-assert", "non-iso-timestamp");
        var goldenPath = scope.WriteFile(
            "expected.json",
            """
            {
              "generatedAtUtc": "<timestamp>"
            }
            """);

        var exception = Assert.Throws<XunitException>(
            () => JsonGoldenFileAssert.MatchesFile(
                goldenPath,
                """
                {
                  "generatedAtUtc": "03/06/2026 00:00:00 +00:00"
                }

                """,
                new JsonGoldenFileNormalization().NormalizeTimestampProperty("generatedAtUtc")));

        Assert.Contains("generatedAtUtc", exception.Message, StringComparison.Ordinal);
        Assert.Contains("ISO-8601", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void MatchesFile_WithRejectedTimestampValidator_Throws ()
    {
        using var scope = TestDirectories.CreateTempScope("json-golden-file-assert", "timestamp-validator");
        var goldenPath = scope.WriteFile(
            "expected.json",
            """
            {
              "generatedAtUtc": "<generatedAtUtc>"
            }
            """);

        var exception = Assert.Throws<XunitException>(
            () => JsonGoldenFileAssert.MatchesFile(
                goldenPath,
                """
                {
                  "generatedAtUtc": "2026-03-06T09:00:00+09:00"
                }

                """,
                new JsonGoldenFileNormalization().NormalizeTimestampProperty(
                    "generatedAtUtc",
                    "<generatedAtUtc>",
                    static timestamp => timestamp.Offset == TimeSpan.Zero,
                    "an ISO-8601 UTC timestamp")));

        Assert.Contains("generatedAtUtc", exception.Message, StringComparison.Ordinal);
        Assert.Contains("UTC", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void MatchesFile_WithSameLiteralOutsideTargetProperty_Throws ()
    {
        using var scope = TestDirectories.CreateTempScope("json-golden-file-assert", "same-literal-outside-target");
        var goldenPath = scope.WriteFile(
            "expected.json",
            """
            {
              "requestId": "<requestId>",
              "copy": "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62"
            }
            """);

        var exception = Assert.Throws<XunitException>(
            () => JsonGoldenFileAssert.MatchesFile(
                goldenPath,
                """
                {
                  "requestId": "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
                  "copy": "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62"
                }

                """,
                new JsonGoldenFileNormalization().NormalizeGuidProperty("requestId", "<requestId>")));

        Assert.Contains("selected for normalization", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62", exception.Message, StringComparison.Ordinal);
    }
}
