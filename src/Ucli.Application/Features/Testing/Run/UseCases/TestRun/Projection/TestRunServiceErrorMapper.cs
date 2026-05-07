using MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;
using MackySoft.Ucli.Application.Features.Testing.Run.Common.Contracts;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Testing.Run.UseCases.TestRun.Projection;

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
                ExecutionErrorCodeMapper.ToCode(error),
                runId,
                artifactsDir,
                summaryJsonPath),
            ExecutionErrorKind.Timeout => TestRunServiceResult.ToolError(
                error.Message,
                ExecutionErrorCodeMapper.ToCode(error),
                runId,
                artifactsDir,
                summaryJsonPath),
            _ => TestRunServiceResult.InfraError(
                error.Message,
                ExecutionErrorCodeMapper.ToCode(error),
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
                UcliCoreErrorCodes.InternalError);
        }

        var hasInternalError = errors.Any(static error => error.Kind == ExecutionErrorKind.InternalError);
        var errorCode = ResolveConfigurationErrorCode(errors, hasInternalError);
        var message = string.Join(" | ", errors.Select(static error => error.Message));

        return hasInternalError
            ? TestRunServiceResult.InfraError(message, errorCode)
            : TestRunServiceResult.InvalidInput(message, errorCode);
    }

    private static UcliErrorCode ResolveConfigurationErrorCode (
        IReadOnlyList<ExecutionError> errors,
        bool hasInternalError)
    {
        if (hasInternalError)
        {
            return UcliCoreErrorCodes.InternalError;
        }

        return errors.Count == 1
            ? ExecutionErrorCodeMapper.ToCode(errors[0])
            : UcliCoreErrorCodes.InvalidArgument;
    }
}
