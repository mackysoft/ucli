using MackySoft.Ucli.Cli;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.TestRun.Artifacts;

namespace MackySoft.Ucli.TestRun.Service.Mapping;

/// <summary> Maps shared execution error contracts into command-facing test-run service results. </summary>
internal static class TestRunServiceErrorMapper
{
    /// <summary> Maps one execution error into service result with optional artifacts context. </summary>
    /// <param name="error"> The execution error. </param>
    /// <param name="session"> The optional artifacts session. </param>
    /// <returns> The mapped service result. </returns>
    public static TestRunServiceResult MapExecutionError (
        ExecutionError error,
        ArtifactsSession? session = null)
    {
        ArgumentNullException.ThrowIfNull(error);

        var runId = session?.RunId;
        var artifactsDir = session?.Paths.ArtifactsDir;
        var summaryJsonPath = session?.Paths.SummaryJsonPath;

        return error.Kind switch
        {
            ExecutionErrorKind.InvalidArgument => TestRunServiceResult.InvalidInput(
                error.Message,
                IpcErrorCodes.InvalidArgument,
                runId,
                artifactsDir,
                summaryJsonPath),
            ExecutionErrorKind.Timeout => TestRunServiceResult.ToolError(
                error.Message,
                CliErrorCodes.IpcTimeout,
                runId,
                artifactsDir,
                summaryJsonPath),
            _ => TestRunServiceResult.InfraError(
                error.Message,
                IpcErrorCodes.InternalError,
                runId,
                artifactsDir,
                summaryJsonPath),
        };
    }

    /// <summary> Maps configuration resolution errors into one service result. </summary>
    /// <param name="errors"> The configuration resolution errors. </param>
    /// <returns> The mapped service result. </returns>
    public static TestRunServiceResult MapConfigurationErrors (IReadOnlyList<ExecutionError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        if (errors.Count == 0)
        {
            return TestRunServiceResult.InfraError(
                "Unexpected error while resolving run configuration.",
                IpcErrorCodes.InternalError);
        }

        var hasInternalError = errors.Any(static error => error.Kind == ExecutionErrorKind.InternalError);
        var errorCode = hasInternalError
            ? IpcErrorCodes.InternalError
            : IpcErrorCodes.InvalidArgument;
        var message = string.Join(" | ", errors.Select(static error => error.Message));

        return hasInternalError
            ? TestRunServiceResult.InfraError(message, errorCode)
            : TestRunServiceResult.InvalidInput(message, errorCode);
    }
}