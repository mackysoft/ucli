using MackySoft.Ucli.Cli;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.TestRun.Artifacts;
using MackySoft.Ucli.TestRun.Execution;
using MackySoft.Ucli.TestRun.Results;
using MackySoft.Ucli.TestRun.Service.Pipeline;

namespace MackySoft.Ucli.TestRun.Service.Mapping;

/// <summary> Implements mapping from pipeline outcomes to command-facing test-run results. </summary>
internal sealed class TestRunResultMapper : ITestRunResultMapper
{
    /// <summary> Maps one execution pipeline result into service output. </summary>
    /// <param name="pipelineResult"> The execution pipeline result. </param>
    /// <returns> The mapped service output. </returns>
    public TestRunServiceResult Map (TestRunExecutionPipelineResult pipelineResult)
    {
        ArgumentNullException.ThrowIfNull(pipelineResult);

        if (pipelineResult.Error is not null)
        {
            return TestRunServiceErrorMapper.MapExecutionError(pipelineResult.Error, pipelineResult.Session);
        }

        if (!pipelineResult.IsSuccess)
        {
            return TestRunServiceResult.InfraError(
                "Unexpected execution pipeline state.",
                IpcErrorCodes.InternalError,
                pipelineResult.Session?.RunId,
                pipelineResult.Session?.Paths.ArtifactsDir,
                pipelineResult.Session?.Paths.SummaryJsonPath);
        }

        return CreateExecutionResult(
            pipelineResult.UnityExecutionResult!,
            pipelineResult.ConversionResult!,
            pipelineResult.Session!);
    }

    /// <summary> Creates final output from execution and conversion outcomes. </summary>
    /// <param name="unityExecutionResult"> The Unity execution result. </param>
    /// <param name="conversionResult"> The results conversion result. </param>
    /// <param name="session"> The prepared artifacts session. </param>
    /// <returns> The final service result. </returns>
    private static TestRunServiceResult CreateExecutionResult (
        UnityTestExecutionResult unityExecutionResult,
        UnityResultsConversionResult conversionResult,
        ArtifactsSession session)
    {
        if (!unityExecutionResult.IsSuccess)
        {
            var errorCode = unityExecutionResult.FailureKind switch
            {
                UnityTestExecutionFailureKind.Canceled => CliErrorCodes.Canceled,
                UnityTestExecutionFailureKind.TimedOut => CliErrorCodes.IpcTimeout,
                _ when !string.IsNullOrWhiteSpace(unityExecutionResult.ErrorCode) => unityExecutionResult.ErrorCode!,
                _ => TestRunErrorCodes.UnityTestExecutionFailed,
            };

            return TestRunServiceResult.ToolError(
                unityExecutionResult.ErrorMessage ?? "Unity test execution failed.",
                errorCode,
                runId: session.RunId,
                artifactsDir: session.Paths.ArtifactsDir,
                summaryJsonPath: session.Paths.SummaryJsonPath);
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
                    CliErrorCodes.Canceled,
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

        return TestRunServiceResult.Pass(
            message: "Unity test execution completed.",
            runId: session.RunId,
            artifactsDir: session.Paths.ArtifactsDir,
            summaryJsonPath: session.Paths.SummaryJsonPath);
    }
}