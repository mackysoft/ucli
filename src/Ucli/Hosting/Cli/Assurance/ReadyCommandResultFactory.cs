using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Common.Projection;

namespace MackySoft.Ucli.Hosting.Cli.Assurance;

/// <summary> Creates command-level JSON results from <c>ready</c> execution results. </summary>
internal static class ReadyCommandResultFactory
{
    /// <summary> Creates one command result for <c>ready</c>. </summary>
    public static CommandResult Create (ReadyExecutionResult executionResult)
    {
        ArgumentNullException.ThrowIfNull(executionResult);

        if (executionResult.IsSuccess)
        {
            return CreateSuccess(executionResult);
        }

        return CommandFailureProjector.Create(
            UcliCommandNames.Ready,
            executionResult.Message,
            CreateFailurePayload(executionResult),
            executionResult.Errors);
    }

    /// <summary> Creates one command result for <c>ready</c> from a normalized execution error. </summary>
    public static CommandResult CreateExecutionError (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return Create(ReadyExecutionResult.Failure(error));
    }

    private static object? CreateFailurePayload (ReadyExecutionResult executionResult)
    {
        if (executionResult.Project is null && executionResult.Errors.All(static error => error.StartupFailure is null))
        {
            return null;
        }

        var payload = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (executionResult.Project is not null)
        {
            payload["project"] = ProjectIdentityPayloadProjector.Create(executionResult.Project);
        }

        StartupFailurePayloadProjector.AppendFromFailures(payload, executionResult.Errors);
        return payload;
    }

    private static CommandResult CreateSuccess (ReadyExecutionResult executionResult)
    {
        var output = executionResult.Output!;
        return new CommandResult(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            Command: UcliCommandNames.Ready,
            Status: IpcProtocol.StatusOk,
            ExitCode: string.Equals(output.Verdict, ReadyVerdictValues.Pass, StringComparison.Ordinal)
                ? (int)CliExitCode.Success
                : 1,
            Message: executionResult.Message,
            Payload: output,
            Errors: []);
    }
}
