using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.OperationCatalog.Common.Contracts;

namespace MackySoft.Ucli.Hosting.Cli;

/// <summary> Creates command-level JSON results from <c>ops</c> service results. </summary>
internal static class OpsCommandResultFactory
{
    /// <summary> Creates one command result for <c>ops list</c>. </summary>
    /// <param name="serviceResult"> The service result. </param>
    /// <returns> The command result serialized to stdout. </returns>
    public static CommandResult CreateList (OpsListServiceResult serviceResult)
    {
        ArgumentNullException.ThrowIfNull(serviceResult);

        if (serviceResult.IsSuccess)
        {
            return CommandResult.Success(
                command: UcliCommandNames.OpsList,
                message: serviceResult.Message,
                payload: new
                {
                    operations = serviceResult.Output!.Operations,
                    readIndex = serviceResult.Output.ReadIndex,
                });
        }

        return CreateFailure(UcliCommandNames.OpsList, serviceResult.Message, serviceResult.ErrorCode);
    }

    /// <summary> Creates one command result for <c>ops describe</c>. </summary>
    /// <param name="serviceResult"> The service result. </param>
    /// <returns> The command result serialized to stdout. </returns>
    public static CommandResult CreateDescribe (OpsDescribeServiceResult serviceResult)
    {
        ArgumentNullException.ThrowIfNull(serviceResult);

        if (serviceResult.IsSuccess)
        {
            return CommandResult.Success(
                command: UcliCommandNames.OpsDescribe,
                message: serviceResult.Message,
                payload: new
                {
                    operation = serviceResult.Output!.Operation,
                    readIndex = serviceResult.Output.ReadIndex,
                });
        }

        return CreateFailure(UcliCommandNames.OpsDescribe, serviceResult.Message, serviceResult.ErrorCode);
    }

    private static CommandResult CreateFailure (
        string command,
        string message,
        string? errorCode)
    {
        return new CommandResult(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            Command: command,
            Status: IpcProtocol.StatusError,
            ExitCode: (int)ResolveExitCode(errorCode),
            Message: message,
            Payload: new { },
            Errors:
            [
                new CommandError(
                    ResolveErrorCode(errorCode),
                    message,
                    null),
            ]);
    }

    private static CliExitCode ResolveExitCode (string? errorCode)
    {
        return string.Equals(errorCode, IpcErrorCodes.InvalidArgument, StringComparison.Ordinal)
            ? CliExitCode.InvalidArgument
            : CliExitCode.ToolError;
    }

    private static string ResolveErrorCode (string? errorCode)
    {
        return string.IsNullOrWhiteSpace(errorCode)
            ? IpcErrorCodes.InternalError
            : errorCode;
    }
}