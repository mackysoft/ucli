using MackySoft.Ucli.Application.Features.Status.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Status;

/// <summary> Creates command-level JSON results from status execution results. </summary>
internal static class StatusCommandResultFactory
{
    /// <summary> Creates one command result for <c>status</c>. </summary>
    /// <param name="executionResult"> The status execution result. </param>
    /// <returns> The command result serialized to stdout. </returns>
    public static CommandResult Create (StatusExecutionResult executionResult)
    {
        ArgumentNullException.ThrowIfNull(executionResult);

        if (executionResult.IsSuccess)
        {
            var output = executionResult.Output!;
            return CommandResult.Success(
                command: UcliCommandNames.Status,
                message: "uCLI status retrieval completed.",
                payload: new
                {
                    daemonStatus = StatusDaemonStateCodec.ToValue(output.DaemonStatus),
                    unityVersion = output.UnityVersion,
                    serverVersion = output.ServerVersion,
                    lifecycleState = output.LifecycleState,
                    blockingReason = output.BlockingReason,
                    compileState = output.CompileState,
                    compileGeneration = output.CompileGeneration,
                    domainReloadGeneration = output.DomainReloadGeneration,
                    canAcceptExecutionRequests = output.CanAcceptExecutionRequests,
                    editorMode = output.EditorMode,
                    observedAtUtc = output.ObservedAtUtc,
                    actionRequired = output.ActionRequired,
                    primaryDiagnostic = output.PrimaryDiagnostic,
                    timeoutMilliseconds = output.TimeoutMilliseconds,
                });
        }

        return CommandResultFactory.FromExecutionError(UcliCommandNames.Status, executionResult.Error!);
    }
}
