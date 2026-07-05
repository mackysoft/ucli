namespace MackySoft.Ucli.Tests;

using System.Text.Json;
using MackySoft.Tests;
using Xunit.Sdk;
using static JsonGoldenFileAssertPathNormalizationTestSupport;

public sealed class JsonGoldenFileAssertPathNormalizationTests
{
    [Fact]
    [Trait("Size", "Medium")]
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
    [Trait("Size", "Medium")]
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
    [Trait("Size", "Medium")]
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
    [Trait("Size", "Medium")]
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
    [Trait("Size", "Medium")]
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
    [Trait("Size", "Medium")]
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
}
