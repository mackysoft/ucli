namespace MackySoft.Ucli.Tests;

using MackySoft.Tests;
using Xunit.Sdk;

public sealed class JsonGoldenFileAssertBasicTests
{
    [Fact]
    [Trait("Size", "Medium")]
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
    [Trait("Size", "Medium")]
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
    [Trait("Size", "Medium")]
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
    [Trait("Size", "Medium")]
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
    [Trait("Size", "Medium")]
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
