using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.TestRun.Artifacts;
using MackySoft.Ucli.TestRun.Results;

namespace MackySoft.Ucli.Tests;

public sealed class UnityResultsConverterTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Convert_WithValidXml_WritesResultsAndSummary ()
    {
        using var scope = CreateSessionScope("valid", out var session);
        scope.WriteFile(
            "results.xml",
            """
            <test-run>
              <test-case fullname="Cafe.Tests.Passed" result="Passed" duration="0.2">
                <properties>
                  <property name="Category" value="smoke" />
                  <property name="Category" value="smoke" />
                </properties>
              </test-case>
              <test-case fullname="Cafe.Tests.Failed" result="Failed" duration="1.0">
                <failure>
                  <message>assert failed</message>
                  <stack-trace>stack trace</stack-trace>
                </failure>
              </test-case>
            </test-run>
            """);

        var converter = new UnityResultsConverter();

        var result = await converter.Convert(session, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.HasFailedTests);
        Assert.True(File.Exists(session.Paths.ResultsJsonPath));
        Assert.True(File.Exists(session.Paths.SummaryJsonPath));

        using var summaryDocument = JsonDocument.Parse(File.ReadAllText(session.Paths.SummaryJsonPath));
        Assert.Equal("fail", summaryDocument.RootElement.GetProperty("status").GetString());
        Assert.Equal(1, summaryDocument.RootElement.GetProperty("counts").GetProperty("failed").GetInt32());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Convert_WithInvalidXml_ReturnsInvalidResultsXmlFailure ()
    {
        using var scope = CreateSessionScope("invalid-xml", out var session);
        scope.WriteFile("results.xml", "<test-run><test-case");

        var converter = new UnityResultsConverter();

        var result = await converter.Convert(session, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityResultsConversionFailureKind.InvalidResultsXml, result.FailureKind);
        Assert.Contains("Failed to parse results.xml", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    public async Task Convert_WithNonFiniteDuration_ReturnsInvalidResultsXmlFailure (string duration)
    {
        using var scope = CreateSessionScope("non-finite-duration", out var session);
        scope.WriteFile(
            "results.xml",
            $"""
            <test-run>
              <test-case fullname="Cafe.Tests.Sample" result="Passed" duration="{duration}" />
            </test-run>
            """);

        var converter = new UnityResultsConverter();

        var result = await converter.Convert(session, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityResultsConversionFailureKind.InvalidResultsXml, result.FailureKind);
        Assert.Contains("Failed to parse results.xml", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Convert_WithFailedSuiteAndNoTestCase_ReturnsFailedSummary ()
    {
        using var scope = CreateSessionScope("failed-suite-no-case", out var session);
        scope.WriteFile(
            "results.xml",
            """
            <test-run>
              <test-suite fullname="Cafe.Tests" result="Failed" />
            </test-run>
            """);

        var converter = new UnityResultsConverter();

        var result = await converter.Convert(session, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.HasFailedTests);

        using var summaryDocument = JsonDocument.Parse(File.ReadAllText(session.Paths.SummaryJsonPath));
        Assert.Equal("fail", summaryDocument.RootElement.GetProperty("status").GetString());
        Assert.Equal(0, summaryDocument.RootElement.GetProperty("counts").GetProperty("failed").GetInt32());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Convert_WithResultsXmlReadFailure_ReturnsResultsXmlReadFailed ()
    {
        using var scope = CreateSessionScope("read-failure", out var session);

        var converter = new UnityResultsConverter();

        var result = await converter.Convert(session, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityResultsConversionFailureKind.ResultsXmlReadFailed, result.FailureKind);
        Assert.Contains("Failed to read results.xml", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Convert_WithOutputWriteFailure_ReturnsOutputWriteFailed ()
    {
        using var scope = CreateSessionScope("write-failure", out var session, artifactsDirectoryPath =>
        {
            var missingDirectory = Path.Combine(artifactsDirectoryPath, "missing");
            return new ArtifactPaths(
                MetaJsonPath: Path.Combine(artifactsDirectoryPath, "meta.json"),
                ResultsXmlPath: Path.Combine(artifactsDirectoryPath, "results.xml"),
                EditorLogPath: Path.Combine(artifactsDirectoryPath, "editor.log"),
                ResultsJsonPath: Path.Combine(missingDirectory, "results.json"),
                SummaryJsonPath: Path.Combine(missingDirectory, "summary.json"));
        });
        scope.WriteFile(
            "results.xml",
            "<test-run><test-case fullname=\"Cafe.Tests.Passed\" result=\"Passed\" duration=\"0\" /></test-run>");

        var converter = new UnityResultsConverter();

        var result = await converter.Convert(session, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityResultsConversionFailureKind.OutputWriteFailed, result.FailureKind);
        Assert.Contains("Failed to write results artifacts", result.ErrorMessage, StringComparison.Ordinal);
    }

    private static TestDirectoryScope CreateSessionScope (
        string testCaseName,
        out ArtifactsSession session,
        Func<string, ArtifactPaths>? artifactPathsFactory = null)
    {
        var scope = TestDirectories.CreateTempScope("unity-results-converter", testCaseName);
        var artifactsDirectoryPath = scope.FullPath;

        var artifactPaths = artifactPathsFactory?.Invoke(artifactsDirectoryPath) ?? new ArtifactPaths(
            MetaJsonPath: Path.Combine(artifactsDirectoryPath, "meta.json"),
            ResultsXmlPath: Path.Combine(artifactsDirectoryPath, "results.xml"),
            EditorLogPath: Path.Combine(artifactsDirectoryPath, "editor.log"),
            ResultsJsonPath: Path.Combine(artifactsDirectoryPath, "results.json"),
            SummaryJsonPath: Path.Combine(artifactsDirectoryPath, "summary.json"));

        session = new ArtifactsSession(
            RunId: "20260301_120000Z_abcd1234",
            ArtifactsDir: artifactsDirectoryPath,
            Paths: artifactPaths,
            StartedAtUtc: DateTimeOffset.UtcNow);
        return scope;
    }
}