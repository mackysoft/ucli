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
            new JsonGoldenFileNormalization().NormalizeGuidProperty("requestId", "<requestId>"));
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
                new JsonGoldenFileNormalization().NormalizeGuidProperty("requestId", "<requestId>")));

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
            new JsonGoldenFileNormalization().NormalizePathPrefix(workspacePath, "<workspace>"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void MatchesFile_WithDefaultEscapedPathPrefixNormalization_ReplacesToken ()
    {
        using var scope = TestDirectories.CreateTempScope("json-golden-file-assert", "escaped-path-prefix");
        var workspacePath = scope.CreateDirectory("workspace");
        var actualPath = Path.Combine(workspacePath, "Folder<Angle>", "config.json");
        var actualPathLiteral = JsonSerializer.Serialize(actualPath);
        var goldenPath = scope.WriteFile(
            "expected.json",
            """
            {
              "path": "<workspace>/Folder<Angle>/config.json"
            }
            """);

        JsonGoldenFileAssert.MatchesFile(
            goldenPath,
            $$"""
            {
              "path": {{actualPathLiteral}}
            }

            """,
            new JsonGoldenFileNormalization().NormalizePathPrefix(workspacePath, "<workspace>"));
    }

    [Fact]
    [Trait("Size", "Small")]
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
    [Trait("Size", "Small")]
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
    [Trait("Size", "Small")]
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
    [Trait("Size", "Small")]
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

    [Fact]
    [Trait("Size", "Small")]
    public void MatchesFile_WithAmbiguousPathPrefixNormalization_DoesNotExposeSourcePath ()
    {
        using var scope = TestDirectories.CreateTempScope("json-golden-file-assert", "ambiguous-path-prefix");
        var workspacePath = scope.CreateDirectory("workspace");
        var actualPath = Path.Combine(workspacePath, ".ucli", "config.json");
        var actualPathLiteral = JsonSerializer.Serialize(actualPath);
        var goldenPath = scope.WriteFile(
            "expected.json",
            """
            {
              "path": "<workspace>/.ucli/config.json",
              "copy": "kept"
            }
            """);

        var exception = Assert.Throws<XunitException>(
            () => JsonGoldenFileAssert.MatchesFile(
                goldenPath,
                $$"""
                {
                  "path": {{actualPathLiteral}},
                  {{actualPathLiteral}}: "kept"
                }

                """,
                new JsonGoldenFileNormalization().NormalizePathPrefix(workspacePath, "<workspace>")));

        Assert.Contains("selected for normalization", exception.Message, StringComparison.Ordinal);
        Assert.Contains("<workspace>", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(workspacePath, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(actualPath, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void MatchesFile_WithAlternateDirectorySeparators_NormalizesPathPrefix ()
    {
        using var scope = TestDirectories.CreateTempScope("json-golden-file-assert", "alternate-path-separators");
        var workspacePath = scope.CreateDirectory("workspace");
        var alternateWorkspacePath = ReplaceWithAlternateDirectorySeparators(workspacePath);
        var actualPath = alternateWorkspacePath + GetAlternateDirectorySeparator() + ".ucli" + GetAlternateDirectorySeparator() + "config.json";
        var actualPathLiteral = JsonSerializer.Serialize(actualPath);
        var goldenPath = scope.WriteFile(
            "expected.json",
            """
            {
              "path": "<workspace>/.ucli/config.json"
            }
            """);

        JsonGoldenFileAssert.MatchesFile(
            goldenPath,
            $$"""
            {
              "path": {{actualPathLiteral}}
            }

            """,
            new JsonGoldenFileNormalization().NormalizePathPrefix(workspacePath, "<workspace>"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void MatchesFile_WithMixedDirectorySeparators_NormalizesPathPrefix ()
    {
        using var scope = TestDirectories.CreateTempScope("json-golden-file-assert", "mixed-path-separators");
        var workspacePath = scope.CreateDirectory("workspace");
        var mixedWorkspacePath = ReplaceWithMixedDirectorySeparators(workspacePath);
        var actualPath = mixedWorkspacePath + GetAlternateDirectorySeparator() + "Unity Project" + Path.DirectorySeparatorChar + ".ucli" + GetAlternateDirectorySeparator() + "config.json";
        var actualPathLiteral = JsonSerializer.Serialize(actualPath);
        var goldenPath = scope.WriteFile(
            "expected.json",
            """
            {
              "path": "<workspace>/Unity Project/.ucli/config.json"
            }
            """);

        JsonGoldenFileAssert.MatchesFile(
            goldenPath,
            $$"""
            {
              "path": {{actualPathLiteral}}
            }

            """,
            new JsonGoldenFileNormalization().NormalizePathPrefix(workspacePath, "<workspace>"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void MatchesFile_WithMultiplePathPrefixesInSingleStringMismatch_DoesNotExposeSourcePaths ()
    {
        using var scope = TestDirectories.CreateTempScope("json-golden-file-assert", "multiple-path-prefixes");
        var workspacePath = scope.CreateDirectory("workspace");
        var sourcePath = Path.Combine(workspacePath, "input.json");
        var targetPath = Path.Combine(workspacePath, "logs", "output.json");
        var actualMessage = $"paths={sourcePath};{targetPath}";
        var actualMessageLiteral = JsonSerializer.Serialize(actualMessage);
        var goldenPath = scope.WriteFile(
            "expected.json",
            """
            {
              "message": "paths=<workspace>/input.json;<workspace>/logs/missing.json"
            }
            """);

        var exception = Assert.Throws<XunitException>(
            () => JsonGoldenFileAssert.MatchesFile(
                goldenPath,
                $$"""
                {
                  "message": {{actualMessageLiteral}}
                }

                """,
                new JsonGoldenFileNormalization().NormalizePathPrefix(workspacePath, "<workspace>")));

        Assert.Contains("<workspace>/input.json", exception.Message, StringComparison.Ordinal);
        Assert.Contains("<workspace>/logs/output.json", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(workspacePath, exception.Message, StringComparison.Ordinal);
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

    private static string ReplaceWithAlternateDirectorySeparators (string path)
    {
        return path.Replace(Path.DirectorySeparatorChar, GetAlternateDirectorySeparator());
    }

    private static string ReplaceWithMixedDirectorySeparators (string path)
    {
        var result = path.ToCharArray();
        var useAlternate = false;
        for (var i = 0; i < result.Length; i++)
        {
            if (result[i] is not ('/' or '\\'))
            {
                continue;
            }

            result[i] = useAlternate
                ? GetAlternateDirectorySeparator()
                : Path.DirectorySeparatorChar;
            useAlternate = !useAlternate;
        }

        return new string(result);
    }

    private static char GetAlternateDirectorySeparator ()
    {
        return Path.DirectorySeparatorChar == '/'
            ? '\\'
            : '/';
    }
}
