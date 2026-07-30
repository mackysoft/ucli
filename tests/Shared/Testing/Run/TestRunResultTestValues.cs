using MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;
using MackySoft.Ucli.Application.Features.Testing.Run.Results;

namespace MackySoft.Ucli.TestSupport;

internal static class TestRunResultTestValues
{
    public static UnityResultsConversionSuccess CreateConversion (Verdict verdict)
    {
        var normalizedTest = verdict switch
        {
            Verdict.Pass => UnityResultsXmlParseResult.TestValue.Passed(
                "Cafe.Tests.Passed",
                durationMs: 0,
                categories: []),
            Verdict.Fail => UnityResultsXmlParseResult.TestValue.Failed(
                "Cafe.Tests.Failed",
                durationMs: 0,
                categories: [],
                message: "Test failed.",
                stackTrace: string.Empty),
            Verdict.Incomplete => UnityResultsXmlParseResult.TestValue.Skipped(
                "Cafe.Tests.Skipped",
                durationMs: 0,
                categories: []),
            _ => throw new ArgumentOutOfRangeException(nameof(verdict), verdict, "Verdict must be defined."),
        };
        var normalizedResult = UnityResultsXmlParseResult.Create([normalizedTest]);
        return UnityResultsConversionResult.Success(
            TestRunVerdictEvaluation.Evaluate(
                normalizedResult,
                allowEmptyTestRun: false));
    }

    public static TestRunCompletedServiceResult CreateCompleted (
        Verdict verdict,
        ArtifactsSession artifactsSession)
    {
        return TestRunServiceResult.Completed(
            CreateConversion(verdict),
            artifactsSession);
    }

    public static UnityResultsConversionFailure CreateConversionFailure (
        UnityResultsConversionFailureKind failureKind,
        string errorMessage)
    {
        return failureKind switch
        {
            UnityResultsConversionFailureKind.InvalidResultsXml =>
                UnityResultsConversionResult.InvalidResultsXml(errorMessage),
            UnityResultsConversionFailureKind.ResultsXmlReadFailed =>
                UnityResultsConversionResult.ResultsXmlReadFailed(errorMessage),
            UnityResultsConversionFailureKind.OutputWriteFailed =>
                UnityResultsConversionResult.OutputWriteFailed(errorMessage),
            UnityResultsConversionFailureKind.Canceled =>
                UnityResultsConversionResult.Canceled(errorMessage),
            _ => throw new ArgumentOutOfRangeException(
                nameof(failureKind),
                failureKind,
                "Conversion failure kind must be defined."),
        };
    }
}
