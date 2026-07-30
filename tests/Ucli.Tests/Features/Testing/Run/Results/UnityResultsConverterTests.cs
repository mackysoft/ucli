using System.Text.Json;
using MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;
using MackySoft.Ucli.Application.Features.Testing.Run.Results;
using MackySoft.Ucli.Contracts.Testing;
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
            <test-run testcasecount="2" total="2" passed="1" failed="1" skipped="0" inconclusive="0" result="Failed">
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

        var result = await converter.ConvertAsync(session, allowEmptyTestRun: false, CancellationToken.None);

        var success = Assert.IsType<UnityResultsConversionSuccess>(result);
        Assert.Equal(Verdict.Fail, success.Verdict);
        Assert.True(File.Exists(session.Paths.ResultsJsonPath.Value));
        Assert.True(File.Exists(session.Paths.SummaryJsonPath.Value));

        using var resultsDocument = JsonDocument.Parse(File.ReadAllText(session.Paths.ResultsJsonPath.Value));
        Assert.Equal(RunIdTestValues.TestText, resultsDocument.RootElement.GetProperty("runId").GetString());
        Assert.Collection(
            resultsDocument.RootElement.GetProperty("tests").EnumerateArray(),
            test => Assert.Equal(
                TextVocabulary.GetText(TestCaseResult.Pass),
                test.GetProperty("outcome").GetString()),
            test => Assert.Equal(
                TextVocabulary.GetText(TestCaseResult.Fail),
                test.GetProperty("outcome").GetString()));
        using var summaryDocument = JsonDocument.Parse(File.ReadAllText(session.Paths.SummaryJsonPath.Value));
        Assert.Equal(RunIdTestValues.TestText, summaryDocument.RootElement.GetProperty("runId").GetString());
        Assert.Equal(
            TextVocabulary.GetText(Verdict.Fail),
            summaryDocument.RootElement.GetProperty("verdict").GetString());
        Assert.False(summaryDocument.RootElement.GetProperty("allowEmptyTestRun").GetBoolean());
        Assert.Equal(1, summaryDocument.RootElement.GetProperty("counts").GetProperty("failed").GetInt32());
        Assert.Collection(
            summaryDocument.RootElement.GetProperty("topFailures").EnumerateArray(),
            failure =>
            {
                Assert.Equal("Cafe.Tests.Failed", failure.GetProperty("fullName").GetString());
                Assert.Equal("assert failed", failure.GetProperty("message").GetString());
                Assert.Equal("stack trace", failure.GetProperty("stackTrace").GetString());
            });
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Convert_WithEmptyTestRunAndEmptyNotAllowed_WritesIncompleteVerdict ()
    {
        using var scope = CreateSessionScope("empty-test-run", out var session);
        scope.WriteFile("results.xml", "<test-run />");

        var converter = CreateConverter();

        var result = await converter.ConvertAsync(session, allowEmptyTestRun: false, CancellationToken.None);

        var success = Assert.IsType<UnityResultsConversionSuccess>(result);
        Assert.Equal(Verdict.Incomplete, success.Verdict);
        Assert.Equal(0, success.ReportedTestCaseCount);

        using var summaryDocument = JsonDocument.Parse(File.ReadAllText(session.Paths.SummaryJsonPath.Value));
        Assert.Equal(
            TextVocabulary.GetText(Verdict.Incomplete),
            summaryDocument.RootElement.GetProperty("verdict").GetString());
        Assert.Equal(0, summaryDocument.RootElement.GetProperty("counts").GetProperty("passed").GetInt32());
        Assert.Equal(0, summaryDocument.RootElement.GetProperty("counts").GetProperty("failed").GetInt32());
        Assert.Equal(0, summaryDocument.RootElement.GetProperty("counts").GetProperty("skipped").GetInt32());
        Assert.Equal(0, summaryDocument.RootElement.GetProperty("counts").GetProperty("inconclusive").GetInt32());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Convert_WithEmptyTestRunAndEmptyAllowed_WritesPassVerdict ()
    {
        using var scope = CreateSessionScope("allowed-empty-test-run", out var session);
        scope.WriteFile("results.xml", "<test-run />");

        var converter = CreateConverter();

        var result = await converter.ConvertAsync(session, allowEmptyTestRun: true, CancellationToken.None);

        var success = Assert.IsType<UnityResultsConversionSuccess>(result);
        Assert.Equal(Verdict.Pass, success.Verdict);
        using var summaryDocument = JsonDocument.Parse(File.ReadAllText(session.Paths.SummaryJsonPath.Value));
        Assert.Equal(
            TextVocabulary.GetText(Verdict.Pass),
            summaryDocument.RootElement.GetProperty("verdict").GetString());
        Assert.True(summaryDocument.RootElement.GetProperty("allowEmptyTestRun").GetBoolean());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Convert_WithSkippedOrInconclusiveTests_WritesIncompleteVerdict ()
    {
        using var scope = CreateSessionScope("incomplete-test-run", out var session);
        scope.WriteFile(
            "results.xml",
            """
            <test-run result="Inconclusive">
              <test-case fullname="Cafe.Tests.Skipped" result="Skipped" duration="0" />
              <test-case fullname="Cafe.Tests.Inconclusive" result="Inconclusive" duration="0" />
            </test-run>
            """);

        var converter = CreateConverter();

        var result = await converter.ConvertAsync(session, allowEmptyTestRun: true, CancellationToken.None);

        var success = Assert.IsType<UnityResultsConversionSuccess>(result);
        Assert.Equal(Verdict.Incomplete, success.Verdict);
        using var resultsDocument = JsonDocument.Parse(File.ReadAllText(session.Paths.ResultsJsonPath.Value));
        Assert.Collection(
            resultsDocument.RootElement.GetProperty("tests").EnumerateArray(),
            test => Assert.Equal(
                TextVocabulary.GetText(TestCaseResult.Skipped),
                test.GetProperty("outcome").GetString()),
            test => Assert.Equal(
                TextVocabulary.GetText(TestCaseResult.Inconclusive),
                test.GetProperty("outcome").GetString()));
        using var summaryDocument = JsonDocument.Parse(File.ReadAllText(session.Paths.SummaryJsonPath.Value));
        Assert.Equal(
            TextVocabulary.GetText(Verdict.Incomplete),
            summaryDocument.RootElement.GetProperty("verdict").GetString());
        Assert.Equal(1, summaryDocument.RootElement.GetProperty("counts").GetProperty("skipped").GetInt32());
        Assert.Equal(1, summaryDocument.RootElement.GetProperty("counts").GetProperty("inconclusive").GetInt32());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Convert_WithInvalidXml_ReturnsInvalidResultsXmlFailure ()
    {
        using var scope = CreateSessionScope("invalid-xml", out var session);
        scope.WriteFile("results.xml", "<test-run><test-case");

        var converter = CreateConverter();

        var result = await converter.ConvertAsync(session, allowEmptyTestRun: false, CancellationToken.None);

        var failure = Assert.IsType<UnityResultsConversionFailure>(result);
        Assert.Equal(UnityResultsConversionFailureKind.InvalidResultsXml, failure.FailureKind);
        Assert.Contains("Failed to parse results.xml", failure.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Convert_WithUnknownTestResult_ReturnsInvalidResultsXmlFailure ()
    {
        using var scope = CreateSessionScope("unknown-test-result", out var session);
        scope.WriteFile(
            "results.xml",
            """
            <test-run>
              <test-case fullname="Cafe.Tests.Unknown" result="Unexpected" duration="0" />
            </test-run>
            """);

        var converter = CreateConverter();

        var result = await converter.ConvertAsync(session, allowEmptyTestRun: false, CancellationToken.None);

        var failure = Assert.IsType<UnityResultsConversionFailure>(result);
        Assert.Equal(UnityResultsConversionFailureKind.InvalidResultsXml, failure.FailureKind);
        Assert.Contains("unsupported Unity test result", failure.ErrorMessage, StringComparison.Ordinal);
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

        var result = await converter.ConvertAsync(session, allowEmptyTestRun: false, CancellationToken.None);

        var failure = Assert.IsType<UnityResultsConversionFailure>(result);
        Assert.Equal(UnityResultsConversionFailureKind.InvalidResultsXml, failure.FailureKind);
        Assert.Contains("Failed to parse results.xml", failure.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Convert_WithFailedSuiteAndNoFailedCase_ReturnsInvalidResultsXmlFailure ()
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

        var result = await converter.ConvertAsync(session, allowEmptyTestRun: false, CancellationToken.None);

        var failure = Assert.IsType<UnityResultsConversionFailure>(result);
        Assert.Equal(UnityResultsConversionFailureKind.InvalidResultsXml, failure.FailureKind);
        Assert.Contains("not supported by its test-case collection", failure.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Convert_WhenReportedAggregatesDisagreeWithCases_ReturnsInvalidResultsXmlFailure ()
    {
        using var scope = CreateSessionScope("aggregate-mismatch", out var session);
        scope.WriteFile(
            "results.xml",
            """
            <test-run testcasecount="1" total="1" passed="0" failed="1" skipped="0" inconclusive="0" result="Failed">
              <test-case fullname="Cafe.Tests.Passed" result="Passed" duration="0" />
            </test-run>
            """);

        var converter = CreateConverter();

        var result = await converter.ConvertAsync(session, allowEmptyTestRun: false, CancellationToken.None);

        var failure = Assert.IsType<UnityResultsConversionFailure>(result);
        Assert.Equal(UnityResultsConversionFailureKind.InvalidResultsXml, failure.FailureKind);
        Assert.Contains("aggregate attributes do not match", failure.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Convert_WithResultsXmlReadFailure_ReturnsResultsXmlReadFailed ()
    {
        using var scope = CreateSessionScope("read-failure", out var session);

        var converter = CreateConverter();

        var result = await converter.ConvertAsync(session, allowEmptyTestRun: false, CancellationToken.None);

        var failure = Assert.IsType<UnityResultsConversionFailure>(result);
        Assert.Equal(UnityResultsConversionFailureKind.ResultsXmlReadFailed, failure.FailureKind);
        Assert.Contains("Failed to read results.xml", failure.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Convert_WithOutputWriteFailure_ReturnsOutputWriteFailed ()
    {
        using var scope = CreateSessionScope("write-failure", out var session);
        var converter = new UnityResultsConverter(
            new StubUnityResultsXmlParser(CreateParseResult()),
            new ThrowingUnityResultsArtifactWriter(new IOException("disk full")));

        var result = await converter.ConvertAsync(session, allowEmptyTestRun: false, CancellationToken.None);

        var failure = Assert.IsType<UnityResultsConversionFailure>(result);
        Assert.Equal(UnityResultsConversionFailureKind.OutputWriteFailed, failure.FailureKind);
        Assert.Contains("Failed to write results artifacts", failure.ErrorMessage, StringComparison.Ordinal);
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
        return UnityResultsXmlParseResult.Create(
            [
                UnityResultsXmlParseResult.TestValue.Passed(
                    fullName: "Cafe.Tests.Sample",
                    durationMs: 0,
                    categories: []),
            ]);
    }

    private static UnityResultsConverter CreateConverter ()
    {
        return new UnityResultsConverter(
            new UnityResultsXmlParser(),
            new UnityResultsArtifactWriter());
    }

}
