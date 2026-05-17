using MackySoft.Ucli.Application.Features.Assurance.Verify.Contracts;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Vocabulary;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Common.Projection;

namespace MackySoft.Ucli.Hosting.Cli.Assurance;

/// <summary> Creates command-level JSON results from <c>verify</c> execution results. </summary>
internal static class VerifyCommandResultFactory
{
    /// <summary> Creates one command result for <c>verify</c>. </summary>
    public static CommandResult Create (VerifyExecutionResult executionResult)
    {
        ArgumentNullException.ThrowIfNull(executionResult);

        if (executionResult.IsSuccess)
        {
            return CreateSuccess(executionResult);
        }

        return CommandFailureProjector.Create(
            UcliCommandNames.Verify,
            executionResult.Message,
            CreateFailurePayload(executionResult),
            executionResult.Errors);
    }

    /// <summary> Creates one command result for <c>verify</c> from a normalized execution error. </summary>
    public static CommandResult CreateExecutionError (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return Create(VerifyExecutionResult.Failure(error));
    }

    private static object? CreateFailurePayload (VerifyExecutionResult executionResult)
    {
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (executionResult.Project != null)
        {
            payload["project"] = ProjectIdentityPayloadProjector.Create(executionResult.Project);
        }

        StartupFailurePayloadProjector.AppendFromFailures(payload, executionResult.Errors);
        return payload.Count == 0 ? null : payload;
    }

    private static CommandResult CreateSuccess (VerifyExecutionResult executionResult)
    {
        var output = executionResult.Output!;
        return new CommandResult(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            Command: UcliCommandNames.Verify,
            Status: IpcProtocol.StatusOk,
            ExitCode: string.Equals(output.Verdict, VerifyVerdictValues.Pass, StringComparison.Ordinal)
                ? (int)CliExitCode.Success
                : 1,
            Message: executionResult.Message,
            Payload: output,
            Errors: []);
    }
}
