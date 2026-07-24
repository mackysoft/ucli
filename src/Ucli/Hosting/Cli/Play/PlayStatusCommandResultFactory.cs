using MackySoft.Ucli.Application.Features.Play.UseCases.Status;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Common.Projection;

namespace MackySoft.Ucli.Hosting.Cli.Play;

/// <summary> Creates command-level JSON results from Play Mode status execution results. </summary>
internal static class PlayStatusCommandResultFactory
{
    /// <summary> Creates one command result for <c>play status</c>. </summary>
    /// <param name="executionResult"> The Play Mode status execution result. </param>
    /// <returns> The command result serialized to stdout. </returns>
    public static CommandResult Create (PlayStatusExecutionResult executionResult)
    {
        ArgumentNullException.ThrowIfNull(executionResult);

        if (executionResult.IsSuccess)
        {
            var output = executionResult.Output!;
            return CommandResult.Success(
                command: UcliCommandNames.PlayStatus,
                message: "uCLI play status retrieval completed.",
                payload: new
                {
                    project = ProjectIdentityPayloadProjector.Create(output.Project),
                    daemonStatus = TextVocabulary.GetText(output.DaemonStatus),
                    serverVersion = output.ServerVersion,
                    editorMode = output.EditorMode,
                    lifecycleState = output.LifecycleState,
                    blockingReason = output.BlockingReason,
                    compileState = output.CompileState,
                    generations = output.Generations,
                    canAcceptExecutionRequests = output.CanAcceptExecutionRequests,
                    observedAtUtc = output.ObservedAtUtc,
                    actionRequired = output.ActionRequired,
                    primaryDiagnostic = output.PrimaryDiagnostic,
                    playMode = output.PlayMode,
                    timeoutMilliseconds = output.TimeoutMilliseconds,
                });
        }

        return CommandResultFactory.FromExecutionError(UcliCommandNames.PlayStatus, executionResult.Error!);
    }
}
