using MackySoft.Ucli.Application.Features.Play.UseCases.Enter;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Common.Projection;

namespace MackySoft.Ucli.Hosting.Cli.Play;

/// <summary> Creates command-level JSON results from Play Mode enter execution results. </summary>
internal static class PlayEnterCommandResultFactory
{
    /// <summary> Creates one command result for <c>play enter</c>. </summary>
    /// <param name="executionResult"> The Play Mode enter execution result. </param>
    /// <returns> The command result serialized to stdout. </returns>
    public static CommandResult Create (PlayEnterExecutionResult executionResult)
    {
        ArgumentNullException.ThrowIfNull(executionResult);

        if (executionResult.IsSuccess)
        {
            return CommandResult.Success(
                command: UcliCommandNames.PlayEnter,
                message: executionResult.Message,
                payload: CreatePayload(executionResult.Output!));
        }

        var payload = executionResult.Output is null ? null : CreatePayload(executionResult.Output);
        return CommandFailureProjector.Create(
            UcliCommandNames.PlayEnter,
            executionResult.Message,
            payload,
            [executionResult.Error!]);
    }

    private static object CreatePayload (PlayEnterExecutionOutput output)
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

    private static object CreateTransitionPayload (PlayEnterTransitionOutput transition)
    {
        return transition.Result switch
        {
            IpcPlayTransitionResultNames.Entered or IpcPlayTransitionResultNames.AlreadyEntered => new
            {
                transition = transition.Transition,
                result = transition.Result,
                before = transition.Before,
                after = transition.After,
            },
            IpcPlayTransitionResultNames.Timeout or IpcPlayTransitionResultNames.Blocked => new
            {
                transition = transition.Transition,
                result = transition.Result,
                before = transition.Before,
                observed = transition.Observed,
                applicationState = transition.ApplicationState,
            },
            _ => new
            {
                transition = transition.Transition,
                result = transition.Result,
                before = transition.Before,
            },
        };
    }
}
