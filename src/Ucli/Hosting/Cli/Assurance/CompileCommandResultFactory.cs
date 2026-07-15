using MackySoft.Ucli.Application.Features.Assurance.Compile.Contracts;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Common.Projection;

namespace MackySoft.Ucli.Hosting.Cli.Assurance;

/// <summary> Creates command-level JSON results from <c>compile</c> execution results. </summary>
internal static class CompileCommandResultFactory
{
    /// <summary> Creates one command result for <c>compile</c>. </summary>
    public static CommandResult Create (CompileExecutionResult executionResult)
    {
        ArgumentNullException.ThrowIfNull(executionResult);

        if (executionResult.IsSuccess)
        {
            return CreateSuccess(executionResult);
        }

        return CommandFailureProjector.Create(
            UcliCommandNames.Compile,
            executionResult.Message,
            CreateFailurePayload(executionResult),
            executionResult.Errors);
    }

    /// <summary> Creates one command result for <c>compile</c> from a normalized execution error. </summary>
    public static CommandResult CreateExecutionError (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return Create(CompileExecutionResult.Failure(error));
    }

    private static object? CreateFailurePayload (CompileExecutionResult executionResult)
    {
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (executionResult.Project != null)
        {
            payload["project"] = ProjectIdentityPayloadProjector.Create(executionResult.Project);
        }

        StartupFailurePayloadProjector.AppendFromFailures(payload, executionResult.Errors);
        return payload.Count == 0 ? null : payload;
    }

    private static CommandResult CreateSuccess (CompileExecutionResult executionResult)
    {
        var output = executionResult.Output!;
        return new CommandResult(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            Command: UcliCommandNames.Compile,
            Status: CommandResultStatus.Ok,
            ExitCode: output.Verdict == AssuranceVerdict.Pass
                ? (int)CliExitCode.Success
                : 1,
            Message: executionResult.Message,
            Payload: output,
            Errors: []);
    }
}
