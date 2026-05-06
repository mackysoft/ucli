using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Shared.Execution;

/// <summary> Resolves application outcomes from machine-readable failure codes. </summary>
internal static class ApplicationFailureOutcomeResolver
{
    /// <summary> Resolves the application outcome represented by one failure code. </summary>
    /// <param name="errorCode"> The machine-readable failure code. </param>
    /// <returns> The application outcome for the specified failure code. </returns>
    public static ApplicationOutcome Resolve (string errorCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);

        return IsInvalidArgumentCode(errorCode)
            ? ApplicationOutcome.InvalidArgument
            : ApplicationOutcome.ToolError;
    }

    /// <summary> Returns whether the failure code represents a caller-correctable invalid argument. </summary>
    /// <param name="errorCode"> The machine-readable failure code. </param>
    /// <returns> <see langword="true" /> when the code maps to <see cref="ApplicationOutcome.InvalidArgument" />; otherwise <see langword="false" />. </returns>
    public static bool IsInvalidArgumentCode (string errorCode)
    {
        return errorCode is IpcErrorCodes.InvalidArgument
            or IpcErrorCodes.PlanTokenRequired
            or IpcErrorCodes.PlanTokenInvalid
            or IpcErrorCodes.PlanTokenExpired
            or IpcErrorCodes.PlanTokenRequestMismatch
            or IpcErrorCodes.StateChangedSincePlan;
    }
}
