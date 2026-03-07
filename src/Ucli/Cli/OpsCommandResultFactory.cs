using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Ops;

namespace MackySoft.Ucli.Cli;

/// <summary> Creates command-level JSON results from <c>ops</c> service results. </summary>
internal static class OpsCommandResultFactory
{
    /// <summary> Creates one command result for <c>ops list</c>. </summary>
    /// <param name="serviceResult"> The service result. </param>
    /// <returns> The command result serialized to stdout. </returns>
    public static CommandResult CreateList (OpsServiceResult<OpsListExecutionOutput> serviceResult)
    {
        ArgumentNullException.ThrowIfNull(serviceResult);
        return Create(
            UcliCommandNames.OpsList,
            serviceResult,
            static output => new
            {
                operations = output.Operations,
                readIndex = output.ReadIndex,
            });
    }

    /// <summary> Creates one command result for <c>ops describe</c>. </summary>
    /// <param name="serviceResult"> The service result. </param>
    /// <returns> The command result serialized to stdout. </returns>
    public static CommandResult CreateDescribe (OpsServiceResult<OpsDescribeExecutionOutput> serviceResult)
    {
        ArgumentNullException.ThrowIfNull(serviceResult);
        return Create(
            UcliCommandNames.OpsDescribe,
            serviceResult,
            static output => new
            {
                operation = output.Operation,
                readIndex = output.ReadIndex,
            });
    }

    private static CommandResult Create<T> (
        string command,
        OpsServiceResult<T> serviceResult,
        Func<T, object> payloadFactory)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(serviceResult);
        ArgumentNullException.ThrowIfNull(payloadFactory);

        if (serviceResult.IsSuccess)
        {
            return CommandResult.Success(
                command: command,
                message: serviceResult.Message,
                payload: payloadFactory(serviceResult.Output!));
        }

        return new CommandResult(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            Command: command,
            Status: IpcProtocol.StatusError,
            ExitCode: (int)ResolveExitCode(serviceResult.ErrorCode),
            Message: serviceResult.Message,
            Payload: new { },
            Errors:
            [
                new CommandError(
                    ResolveErrorCode(serviceResult.ErrorCode),
                    serviceResult.Message,
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