using MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;
using MackySoft.Ucli.Application.Features.Testing.Run.Execution;
using MackySoft.Ucli.Application.Features.Testing.Run.Results;

namespace MackySoft.Ucli.Application.Features.Testing.Run.UseCases.TestRun.Projection;

/// <summary> Implements mapping from pipeline outcomes to command-facing test-run results. </summary>
internal sealed class TestRunResultMapper : ITestRunResultMapper
{
    private const string NoTestsExecutedMessage =
        "Unity test execution completed without reporting any test cases. Check --testFilter, --testCategory, or --assemblyName, or specify --allowEmptyTestRun to accept an empty run.";

    /// <summary> Maps one execution pipeline result into service output. </summary>
    /// <param name="pipelineResult"> The execution pipeline result. </param>
    /// <returns> The mapped service output. </returns>
    public TestRunServiceResult Map (TestRunExecutionPipelineResult pipelineResult)
    {
        ArgumentNullException.ThrowIfNull(pipelineResult);

        if (ShouldPreferPrimaryRunOutcome(pipelineResult))
        {
            return CreateExecutionResult(
                pipelineResult.UnityExecutionResult!,
                pipelineResult.ConversionResult!,
                pipelineResult.Session!,
                pipelineResult.AllowEmptyTestRun);
        }

        if (pipelineResult.Error is not null)
        {
            return TestRunServiceErrorMapper.MapExecutionError(pipelineResult.Error, pipelineResult.Session);
        }

        if (!pipelineResult.IsSuccess)
        {
            return TestRunServiceResult.InfraError(
                "Unexpected execution pipeline state.",
                UcliCoreErrorCodes.InternalError,
                pipelineResult.Session?.RunId,
                pipelineResult.Session?.Paths.ArtifactsDir,
                pipelineResult.Session?.Paths.SummaryJsonPath);
        }

        return CreateExecutionResult(
            pipelineResult.UnityExecutionResult!,
            pipelineResult.ConversionResult!,
            pipelineResult.Session!,
            pipelineResult.AllowEmptyTestRun);
    }

    private static bool ShouldPreferPrimaryRunOutcome (TestRunExecutionPipelineResult pipelineResult)
    {
        // NOTE:
        // When artifact completion fails after Unity execution or results conversion already produced
        // the primary user-facing outcome, preserve that outcome instead of replacing it with a
        // secondary cleanup error.
        return pipelineResult.Error is not null
            && pipelineResult.Session is not null
            && pipelineResult.UnityExecutionResult is not null
            && pipelineResult.ConversionResult is not null
            && (!pipelineResult.UnityExecutionResult.IsSuccess
                || !pipelineResult.ConversionResult.IsSuccess
                || pipelineResult.ConversionResult.HasFailedTests
                || IsNoTestsExecutedOutcome(pipelineResult.ConversionResult, pipelineResult.AllowEmptyTestRun));
    }

    /// <summary> Creates final output from execution and conversion outcomes. </summary>
    /// <param name="unityExecutionResult"> The Unity execution result. </param>
    /// <param name="conversionResult"> The results conversion result. </param>
    /// <param name="session"> The prepared artifacts session. </param>
    /// <returns> The final service result. </returns>
    private static TestRunServiceResult CreateExecutionResult (
        UnityTestExecutionResult unityExecutionResult,
        UnityResultsConversionResult conversionResult,
        ArtifactsSession session,
        bool allowEmptyTestRun)
    {
        if (!unityExecutionResult.IsSuccess)
        {
            if (unityExecutionResult.FailureKind is not { } failureKind)
            {
                return TestRunServiceResult.InfraError(
                    "Unexpected Unity test execution failure state.",
                    UcliCoreErrorCodes.InternalError,
                    runId: session.RunId,
                    artifactsDir: session.Paths.ArtifactsDir,
                    summaryJsonPath: session.Paths.SummaryJsonPath);
            }

            UcliCode errorCode = failureKind switch
            {
                UnityTestExecutionFailureKind.Canceled => ExecutionErrorCodes.Canceled,
                UnityTestExecutionFailureKind.IpcTimedOut => ExecutionErrorCodes.IpcTimeout,
                UnityTestExecutionFailureKind.ProcessTimedOut => TestRunErrorCodes.UnityTestExecutionTimeout,
                _ when unityExecutionResult.ErrorCode is not null => unityExecutionResult.ErrorCode,
                _ => TestRunErrorCodes.UnityTestExecutionFailed,
            };

            return IsUnityExecutionInfrastructureFailure(failureKind)
                ? TestRunServiceResult.InfraError(
                    unityExecutionResult.ErrorMessage ?? "Unity test infrastructure failed.",
                    errorCode,
                    runId: session.RunId,
                    artifactsDir: session.Paths.ArtifactsDir,
                    summaryJsonPath: session.Paths.SummaryJsonPath,
                    startupFailure: unityExecutionResult.StartupFailure)
                : TestRunServiceResult.ToolError(
                    unityExecutionResult.ErrorMessage ?? "Unity test execution failed.",
                    errorCode,
                    runId: session.RunId,
                    artifactsDir: session.Paths.ArtifactsDir,
                    summaryJsonPath: session.Paths.SummaryJsonPath,
                    startupFailure: unityExecutionResult.StartupFailure);
        }

        if (!conversionResult.IsSuccess)
        {
            return conversionResult.FailureKind switch
            {
                UnityResultsConversionFailureKind.OutputWriteFailed => TestRunServiceResult.InfraError(
                    conversionResult.ErrorMessage ?? "Failed to write test result artifacts.",
                    TestRunErrorCodes.TestResultsOutputWriteFailed,
                    runId: session.RunId,
                    artifactsDir: session.Paths.ArtifactsDir,
                    summaryJsonPath: session.Paths.SummaryJsonPath),
                UnityResultsConversionFailureKind.ResultsXmlReadFailed => TestRunServiceResult.InfraError(
                    conversionResult.ErrorMessage ?? "Failed to read Unity results XML.",
                    TestRunErrorCodes.TestResultsXmlReadFailed,
                    runId: session.RunId,
                    artifactsDir: session.Paths.ArtifactsDir,
                    summaryJsonPath: session.Paths.SummaryJsonPath),
                UnityResultsConversionFailureKind.Canceled => TestRunServiceResult.ToolError(
                    conversionResult.ErrorMessage ?? "Unity results conversion was canceled.",
                    ExecutionErrorCodes.Canceled,
                    runId: session.RunId,
                    artifactsDir: session.Paths.ArtifactsDir,
                    summaryJsonPath: session.Paths.SummaryJsonPath),
                _ => TestRunServiceResult.ToolError(
                    conversionResult.ErrorMessage ?? "Unity results XML is invalid.",
                    TestRunErrorCodes.TestResultsXmlInvalid,
                    runId: session.RunId,
                    artifactsDir: session.Paths.ArtifactsDir,
                    summaryJsonPath: session.Paths.SummaryJsonPath),
            };
        }

        if (conversionResult.HasFailedTests)
        {
            return TestRunServiceResult.Fail(
                message: "Unity test execution completed with failed tests.",
                runId: session.RunId,
                artifactsDir: session.Paths.ArtifactsDir,
                summaryJsonPath: session.Paths.SummaryJsonPath);
        }

        if (IsNoTestsExecutedOutcome(conversionResult, allowEmptyTestRun))
        {
            return TestRunServiceResult.InvalidInput(
                message: NoTestsExecutedMessage,
                errorCode: TestRunErrorCodes.TestRunNoTestsExecuted,
                runId: session.RunId,
                artifactsDir: session.Paths.ArtifactsDir,
                summaryJsonPath: session.Paths.SummaryJsonPath);
        }

        return TestRunServiceResult.Pass(
            message: "Unity test execution completed.",
            runId: session.RunId,
            artifactsDir: session.Paths.ArtifactsDir,
            summaryJsonPath: session.Paths.SummaryJsonPath);
    }

    private static bool IsNoTestsExecutedOutcome (
        UnityResultsConversionResult conversionResult,
        bool allowEmptyTestRun)
    {
        return !allowEmptyTestRun
            && conversionResult.IsSuccess
            && !conversionResult.HasFailedTests
            && conversionResult.ReportedTestCaseCount == 0;
    }

    private static bool IsUnityExecutionInfrastructureFailure (UnityTestExecutionFailureKind failureKind)
    {
        return failureKind is UnityTestExecutionFailureKind.IpcTimedOut
            or UnityTestExecutionFailureKind.ProcessTimedOut
            or UnityTestExecutionFailureKind.IpcTransportInterrupted
            or UnityTestExecutionFailureKind.AbnormalExit
            or UnityTestExecutionFailureKind.ArtifactMissing
            or UnityTestExecutionFailureKind.ClientSetupFailed;
    }
}
