using MackySoft.Ucli.Cli;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Execution;

/// <summary> Converts execute IPC responses into normalized CLI-facing models. </summary>
internal static class ExecuteResponseConverter
{
    /// <summary> Converts one execute IPC response into normalized operation results and errors. </summary>
    /// <param name="response"> The Unity IPC response. </param>
    /// <returns> The converted execute response. </returns>
    public static ExecuteResponseConversionResult Convert (IpcResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (!IpcPayloadCodec.TryDeserialize(response.Payload, out IpcExecuteResponse? payload, out var payloadError))
        {
            return CreateFailure(
                [
                    new IpcError(
                        IpcErrorCodes.InternalError,
                        $"Execute response payload is invalid. {payloadError.Message}",
                        null),
                ]);
        }

        if (payload == null || payload.OpResults is null)
        {
            return CreateFailure(
                [
                    new IpcError(
                        IpcErrorCodes.InternalError,
                        "Execute response payload is invalid. The 'opResults' field is missing.",
                        null),
                ]);
        }

        var normalizedErrors = NormalizeErrors(response.Status, response.Errors);
        return new ExecuteResponseConversionResult(
            OpResults: payload.OpResults,
            Errors: normalizedErrors,
            ExitCode: ResolveExitCode(normalizedErrors),
            PlanToken: payload.PlanToken);
    }

    /// <summary> Resolves the CLI exit code from one machine-readable error code. </summary>
    /// <param name="errorCode"> The raw error code. </param>
    /// <returns> The associated CLI exit code. </returns>
    public static int ResolveExitCode (string errorCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);

        return string.Equals(errorCode, IpcErrorCodes.InvalidArgument, StringComparison.Ordinal)
            ? (int)CliExitCode.InvalidArgument
            : (int)CliExitCode.ToolError;
    }

    /// <summary> Resolves the CLI exit code from one machine-readable error collection. </summary>
    /// <param name="errors"> The machine-readable error collection. </param>
    /// <returns> The associated CLI exit code. </returns>
    public static int ResolveExitCode (IReadOnlyList<IpcError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        if (errors.Count == 0)
        {
            return (int)CliExitCode.Success;
        }

        return errors.All(static error => string.Equals(error.Code, IpcErrorCodes.InvalidArgument, StringComparison.Ordinal))
            ? (int)CliExitCode.InvalidArgument
            : (int)CliExitCode.ToolError;
    }

    private static IReadOnlyList<IpcError> NormalizeErrors (
        string? status,
        IReadOnlyList<IpcError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        if (string.Equals(status, IpcProtocol.StatusOk, StringComparison.Ordinal)
            && errors.Count == 0)
        {
            return [];
        }

        if (errors.Count != 0)
        {
            return errors;
        }

        return
        [
            new IpcError(
                IpcErrorCodes.InternalError,
                $"Execute response failed with status '{status}'.",
                null),
        ];
    }

    private static ExecuteResponseConversionResult CreateFailure (IReadOnlyList<IpcError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        return new ExecuteResponseConversionResult(
            OpResults: [],
            Errors: errors,
            ExitCode: ResolveExitCode(errors),
            PlanToken: null);
    }
}