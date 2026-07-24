using System.Text.Json;
using MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;
using MackySoft.Ucli.Application.Features.Testing.Run.Results;
using MackySoft.Ucli.Tests.Helpers.Testing;

namespace MackySoft.Ucli.Tests;

public sealed class UnityResultsConverterTests
{
    [Fact]
    [Trait("Size", "Medium")]
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

        var converter = CreateConverter();

        var result = await converter.ConvertAsync(session, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.HasFailedTests);
        Assert.True(File.Exists(session.Paths.ResultsJsonPath.Value));
        Assert.True(File.Exists(session.Paths.SummaryJsonPath.Value));

        using var resultsDocument = JsonDocument.Parse(File.ReadAllText(session.Paths.ResultsJsonPath.Value));
        Assert.Equal(RunIdTestValues.TestText, resultsDocument.RootElement.GetProperty("runId").GetString());
        using var summaryDocument = JsonDocument.Parse(File.ReadAllText(session.Paths.SummaryJsonPath.Value));
        Assert.Equal(RunIdTestValues.TestText, summaryDocument.RootElement.GetProperty("runId").GetString());
        Assert.Equal("fail", summaryDocument.RootElement.GetProperty("status").GetString());
        Assert.Equal(1, summaryDocument.RootElement.GetProperty("counts").GetProperty("failed").GetInt32());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Convert_WithEmptyTestRun_ReportsZeroTestCases ()
    {
        using var scope = CreateSessionScope("empty-test-run", out var session);
        scope.WriteFile("results.xml", "<test-run />");

        var converter = CreateConverter();

        var result = await converter.ConvertAsync(session, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.HasFailedTests);
        Assert.Equal(0, result.ReportedTestCaseCount);

        using var summaryDocument = JsonDocument.Parse(File.ReadAllText(session.Paths.SummaryJsonPath.Value));
        Assert.Equal("pass", summaryDocument.RootElement.GetProperty("status").GetString());
        Assert.Equal(0, summaryDocument.RootElement.GetProperty("counts").GetProperty("passed").GetInt32());
        Assert.Equal(0, summaryDocument.RootElement.GetProperty("counts").GetProperty("failed").GetInt32());
        Assert.Equal(0, summaryDocument.RootElement.GetProperty("counts").GetProperty("skipped").GetInt32());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Convert_WithInvalidXml_ReturnsInvalidResultsXmlFailure ()
    {
        using var scope = CreateSessionScope("invalid-xml", out var session);
        scope.WriteFile("results.xml", "<test-run><test-case");

        var converter = CreateConverter();

        var result = await converter.ConvertAsync(session, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityResultsConversionFailureKind.InvalidResultsXml, result.FailureKind);
        Assert.Contains("Failed to parse results.xml", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("Size", "Medium")]
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

        var converter = CreateConverter();

        var result = await converter.ConvertAsync(session, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityResultsConversionFailureKind.InvalidResultsXml, result.FailureKind);
        Assert.Contains("Failed to parse results.xml", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
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

        var converter = CreateConverter();

        var result = await converter.ConvertAsync(session, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.HasFailedTests);

        using var summaryDocument = JsonDocument.Parse(File.ReadAllText(session.Paths.SummaryJsonPath.Value));
        Assert.Equal("fail", summaryDocument.RootElement.GetProperty("status").GetString());
        Assert.Equal(0, summaryDocument.RootElement.GetProperty("counts").GetProperty("failed").GetInt32());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Convert_WithResultsXmlReadFailure_ReturnsResultsXmlReadFailed ()
    {
        using var scope = CreateSessionScope("read-failure", out var session);

        var converter = CreateConverter();

        var result = await converter.ConvertAsync(session, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityResultsConversionFailureKind.ResultsXmlReadFailed, result.FailureKind);
        Assert.Contains("Failed to read results.xml", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Convert_WithOutputWriteFailure_ReturnsOutputWriteFailed ()
    {
        using var scope = CreateSessionScope("write-failure", out var session);
        var converter = new UnityResultsConverter(
            new StubUnityResultsXmlParser(CreateParseResult()),
            new ThrowingUnityResultsArtifactWriter(new IOException("disk full")));

        var result = await converter.ConvertAsync(session, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityResultsConversionFailureKind.OutputWriteFailed, result.FailureKind);
        Assert.Contains("Failed to write results artifacts", result.ErrorMessage, StringComparison.Ordinal);
    }

    private static TestDirectoryScope CreateSessionScope (
        string testCaseName,
        out ArtifactsSession session)
    {
        var scope = TestDirectories.CreateTempScope("unity-results-converter", testCaseName);
        var artifactsDirectoryPath = scope.FullPath;

        var artifactPaths = TestArtifactPaths.Create(artifactsDirectoryPath);

        session = new ArtifactsSession(
            runId: RunIdTestValues.Test,
            paths: artifactPaths,
            startedAtUtc: DateTimeOffset.UtcNow);
        return scope;
    }

    private static UnityResultsXmlParseResult CreateParseResult ()
    {
        return new UnityResultsXmlParseResult(
            Counts: new UnityResultsXmlParseResult.CountsValue(1, 0, 0),
            Tests:
            [
                new UnityResultsXmlParseResult.TestValue(
                    FullName: "Cafe.Tests.Sample",
                    Outcome: "passed",
                    DurationMs: 0,
                    Categories: []),
            ],
            TopFailures: [],
            HasSuiteFailure: false);
    }

    private static UnityResultsConverter CreateConverter ()
    {
        return new UnityResultsConverter(
            new UnityResultsXmlParser(),
            new UnityResultsArtifactWriter());
    }

}
