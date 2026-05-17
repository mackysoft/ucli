using MackySoft.Ucli.Application.Features.OperationCatalog.Common.Contracts;
using MackySoft.Ucli.Application.Shared.Execution;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
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

        return CreateFailure(
            UcliCommandNames.OpsList,
            serviceResult.Message,
            serviceResult.ErrorCode,
            serviceResult.StartupFailure);
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

        return CreateFailure(
            UcliCommandNames.OpsDescribe,
            serviceResult.Message,
            serviceResult.ErrorCode,
            serviceResult.StartupFailure);
    }

    private static CommandResult CreateFailure (
        string command,
        string message,
        UcliCode? errorCode,
        StartupFailureDetail? startupFailure)
    {
        var payload = new Dictionary<string, object?>();
        StartupFailurePayloadProjector.Append(payload, startupFailure);
        return CommandFailureProjector.Create(
            command,
            ApplicationFailure.FromCode(errorCode, message, startupFailure: startupFailure),
            payload);
    }
}
