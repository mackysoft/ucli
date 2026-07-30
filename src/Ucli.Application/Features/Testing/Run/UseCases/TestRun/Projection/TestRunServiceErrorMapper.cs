using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Testing.Run.UseCases.TestRun.Projection;

/// <summary> Maps shared execution error contracts into command-facing test-run service results. </summary>
internal static class TestRunServiceErrorMapper
{
    /// <summary> Maps one execution error that occurred before a test run was created. </summary>
    /// <param name="error"> The execution error. </param>
    /// <returns> The mapped service result. </returns>
    public static TestRunBeforeCreationCommandErrorServiceResult MapCommandError (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        var errorCode = ExecutionErrorCodeMapper.ToCode(error);

        if (errorCode == ExecutionErrorCodes.Canceled)
        {
            return TestRunServiceResult.ToolError(ApplicationFailure.Create(
                ApplicationFailureKind.Canceled,
                error.Message,
                errorCode,
                instancePath: null,
                outcome: ApplicationOutcome.ToolError,
                startupFailure: null));
        }

        return error.Kind switch
        {
            ExecutionErrorKind.InvalidArgument => TestRunServiceResult.InvalidInput(
                error.Message,
                errorCode),
            ExecutionErrorKind.Timeout => TestRunServiceResult.ToolError(
                ApplicationFailure.Timeout(error.Message, errorCode, instancePath: null, startupFailure: null)),
            ExecutionErrorKind.InternalError => TestRunServiceResult.InfraError(
                error.Message,
                errorCode),
            _ => throw new ArgumentOutOfRangeException(
                nameof(error),
                error.Kind,
                "Execution error kind must have an explicit test-run projection."),
        };
    }

    /// <summary> Maps configuration resolution errors into one service result. </summary>
    /// <param name="errors"> The configuration resolution errors. </param>
    /// <returns> The mapped service result. </returns>
    public static TestRunBeforeCreationCommandErrorServiceResult MapConfigurationErrors (
        IReadOnlyList<ExecutionError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        if (errors.Count == 0)
        {
            throw new ArgumentException(
                "Configuration resolution failure must contain at least one error.",
                nameof(errors));
        }

        var hasInternalError = errors.Any(static error => error.Kind == ExecutionErrorKind.InternalError);
        var errorCode = ResolveConfigurationErrorCode(errors, hasInternalError);
        var message = string.Join(" | ", errors.Select(static error => error.Message));

        return hasInternalError
            ? TestRunServiceResult.InfraError(
                message,
                errorCode)
            : TestRunServiceResult.InvalidInput(
                message,
                errorCode);
    }

    private static UcliCode ResolveConfigurationErrorCode (
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
