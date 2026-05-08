using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;

namespace MackySoft.Ucli.Application.Shared.Execution;

/// <summary> Resolves application outcomes from machine-readable failure codes. </summary>
internal static class ApplicationFailureOutcomeResolver
{
    /// <summary> Resolves the application outcome represented by one failure code. </summary>
    /// <param name="errorCode"> The machine-readable failure code. </param>
    /// <returns> The application outcome for the specified failure code. </returns>
    public static ApplicationOutcome Resolve (UcliErrorCode errorCode)
    {
        return IsInvalidArgumentCode(errorCode)
            ? ApplicationOutcome.InvalidArgument
            : ApplicationOutcome.ToolError;
    }

    /// <summary> Resolves the application outcome represented by one failure collection. </summary>
    /// <param name="failures"> The classified failures. </param>
    /// <returns> The application outcome represented by the collection. </returns>
    public static ApplicationOutcome Resolve (IReadOnlyList<ApplicationFailure> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);

        if (failures.Count == 0)
        {
            return ApplicationOutcome.Success;
        }

        var hasInvalidArgument = false;
        var hasInfrastructureError = false;
        var hasToolError = false;
        for (var i = 0; i < failures.Count; i++)
        {
            var failure = failures[i];
            if (failure == null)
            {
                throw new ArgumentException("Failure collection must not contain null entries.", nameof(failures));
            }

            switch (failure.Outcome)
            {
                case ApplicationOutcome.InvalidArgument:
                    hasInvalidArgument = true;
                    break;
                case ApplicationOutcome.InfrastructureError:
                    hasInfrastructureError = true;
                    break;
                case ApplicationOutcome.ToolError:
                    hasToolError = true;
                    break;
                default:
                    throw new ArgumentException("Failure outcome must not be success.", nameof(failures));
            }
        }

        if (hasToolError || (hasInvalidArgument && hasInfrastructureError))
        {
            return ApplicationOutcome.ToolError;
        }

        if (hasInvalidArgument)
        {
            return ApplicationOutcome.InvalidArgument;
        }

        if (hasInfrastructureError)
        {
            return ApplicationOutcome.InfrastructureError;
        }

        return ApplicationOutcome.ToolError;
    }

    /// <summary> Returns whether the failure code represents a caller-correctable invalid argument. </summary>
    /// <param name="errorCode"> The machine-readable failure code. </param>
    /// <returns> <see langword="true" /> when the code maps to <see cref="ApplicationOutcome.InvalidArgument" />; otherwise <see langword="false" />. </returns>
    public static bool IsInvalidArgumentCode (UcliErrorCode errorCode)
    {
        return errorCode == UcliCoreErrorCodes.InvalidArgument
            || errorCode == PlanTokenErrorCodes.PlanTokenRequired
            || errorCode == PlanTokenErrorCodes.PlanTokenInvalid
            || errorCode == PlanTokenErrorCodes.PlanTokenExpired
            || errorCode == PlanTokenErrorCodes.PlanTokenRequestMismatch
            || errorCode == PlanTokenErrorCodes.StateChangedSincePlan
            || ValidationErrorCodes.Contains(errorCode)
            || ProjectContextErrorCodes.Contains(errorCode);
    }

}
