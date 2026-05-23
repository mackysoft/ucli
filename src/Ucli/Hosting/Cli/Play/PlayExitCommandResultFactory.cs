using MackySoft.Ucli.Application.Features.Play.UseCases.Exit;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Common.Projection;

namespace MackySoft.Ucli.Hosting.Cli.Play;

/// <summary> Creates command-level JSON results from Play Mode exit execution results. </summary>
internal static class PlayExitCommandResultFactory
{
    /// <summary> Creates one command result for <c>play exit</c>. </summary>
    /// <param name="executionResult"> The Play Mode exit execution result. </param>
    /// <returns> The command result serialized to stdout. </returns>
    public static CommandResult Create (PlayExitExecutionResult executionResult)
    {
        ArgumentNullException.ThrowIfNull(executionResult);

        if (executionResult.IsSuccess)
        {
            return CommandResult.Success(
                command: UcliCommandNames.PlayExit,
                message: executionResult.Message,
                payload: CreatePayload(executionResult.Output!));
        }

        var payload = executionResult.Output is null ? null : CreatePayload(executionResult.Output);
        return CommandFailureProjector.Create(
            UcliCommandNames.PlayExit,
            executionResult.Message,
            payload,
            [executionResult.Error!]);
    }

    private static object CreatePayload (PlayExitExecutionOutput output)
    {
        return new
        {
            project = ProjectIdentityPayloadProjector.Create(output.Project),
            daemonStatus = DaemonStatusPayloadCodec.ToValue(output.DaemonStatus),
            serverVersion = output.ServerVersion,
            editorMode = output.EditorMode,
            lifecycleState = output.LifecycleState,
            blockingReason = output.BlockingReason,
            compileState = output.CompileState,
            compileGeneration = output.CompileGeneration,
            domainReloadGeneration = output.DomainReloadGeneration,
            canAcceptExecutionRequests = output.CanAcceptExecutionRequests,
            observedAtUtc = output.ObservedAtUtc,
            actionRequired = output.ActionRequired,
            primaryDiagnostic = output.PrimaryDiagnostic,
            playMode = output.PlayMode,
            transition = CreateTransitionPayload(output.Transition),
            timeoutMilliseconds = output.TimeoutMilliseconds,
        };
    }

    private static object CreateTransitionPayload (PlayExitTransitionOutput transition)
    {
        return PlayTransitionPayloadProjector.Create(
            transition.Transition,
            transition.Result,
            transition.Before,
            transition.After,
            transition.Observed,
            transition.ApplicationState);
    }
}
