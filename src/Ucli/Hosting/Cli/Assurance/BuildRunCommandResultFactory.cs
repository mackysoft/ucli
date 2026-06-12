using MackySoft.Ucli.Application.Features.Assurance.Build.Contracts;
using MackySoft.Ucli.Application.Features.Assurance.Build.Vocabulary;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Common.Projection;

namespace MackySoft.Ucli.Hosting.Cli.Assurance;

/// <summary> Creates command-level JSON results from <c>build run</c> execution results. </summary>
internal static class BuildRunCommandResultFactory
{
    /// <summary> Creates one command result for <c>build run</c>. </summary>
    public static CommandResult Create (BuildExecutionResult executionResult)
    {
        ArgumentNullException.ThrowIfNull(executionResult);

        return executionResult.IsSuccess
            ? CreateSuccess(executionResult)
            : CommandFailureProjector.Create(
                UcliCommandNames.BuildRun,
                executionResult.Message,
                CreateFailurePayload(executionResult),
                executionResult.Errors);
    }

    /// <summary> Creates one command result for <c>build run</c> from a normalized execution error. </summary>
    public static CommandResult CreateExecutionError (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return Create(BuildExecutionResult.Failure(error));
    }

    private static object? CreateFailurePayload (BuildExecutionResult executionResult)
    {
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (executionResult.Project != null)
        {
            payload["project"] = ProjectIdentityPayloadProjector.Create(executionResult.Project);
        }

        if (executionResult.DirtyState != null)
        {
            payload["dirtyState"] = executionResult.DirtyState;
        }

        StartupFailurePayloadProjector.AppendFromFailures(payload, executionResult.Errors);
        return payload.Count == 0 ? null : payload;
    }

    private static CommandResult CreateSuccess (BuildExecutionResult executionResult)
    {
        var output = executionResult.Output!;
        return new CommandResult(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            Command: UcliCommandNames.BuildRun,
            Status: IpcProtocol.StatusOk,
            ExitCode: string.Equals(output.Verdict, ContractLiteralCodec.ToValue(BuildVerdict.Pass), StringComparison.Ordinal)
                ? (int)CliExitCode.Success
                : 1,
            Message: executionResult.Message,
            Payload: output,
            Errors: []);
    }
}
