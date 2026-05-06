using MackySoft.Ucli.Application.Features.OperationCatalog.Common.Contracts;
using MackySoft.Ucli.Application.Shared.Execution;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Requests;

namespace MackySoft.Ucli.Hosting.Cli.Ops;

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
                    readIndex = ReadIndexInfoPayloadProjector.Create(serviceResult.Output.ReadIndex),
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
                    readIndex = ReadIndexInfoPayloadProjector.Create(serviceResult.Output.ReadIndex),
                });
        }

        return CreateFailure(UcliCommandNames.OpsDescribe, serviceResult.Message, serviceResult.ErrorCode);
    }

    private static CommandResult CreateFailure (
        string command,
        string message,
        UcliErrorCode? errorCode)
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

    private static CliExitCode ResolveExitCode (UcliErrorCode? errorCode)
    {
        return errorCode.HasValue
            && errorCode.Value.IsValid
            && ApplicationFailureOutcomeResolver.IsInvalidArgumentCode(errorCode.Value)
            ? CliExitCode.InvalidArgument
            : CliExitCode.ToolError;
    }

    private static UcliErrorCode ResolveErrorCode (UcliErrorCode? errorCode)
    {
        return !errorCode.HasValue || !errorCode.Value.IsValid
            ? UcliCoreErrorCodes.InternalError
            : errorCode.Value;
    }
}
