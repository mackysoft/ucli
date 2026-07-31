using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.Hosting.Cli.Play.Contracts;

/// <summary>
/// Represents a Play Mode Lifecycle Execution without a typed transition result.
/// </summary>
internal sealed record PlayTransitionStartErrorCommandPayload
    : PlayTransitionErrorCommandPayload
{
    public PlayTransitionStartErrorCommandPayload (
        ProjectIdentityInfo project,
        ExecutionRef lifecycleExecutionRef,
        ExecutionApplicationState applicationState)
    {
        if (!TextVocabulary.IsDefined(applicationState))
        {
            throw new ArgumentOutOfRangeException(
                nameof(applicationState),
                applicationState,
                "Play Mode application state must be defined.");
        }
        if (applicationState == ExecutionApplicationState.PartiallyApplied)
        {
            throw new ArgumentOutOfRangeException(
                nameof(applicationState),
                applicationState,
                "Play Mode transitions do not support a partially applied state.");
        }

        Project = project ?? throw new ArgumentNullException(nameof(project));
        LifecycleExecutionRef = lifecycleExecutionRef
            ?? throw new ArgumentNullException(nameof(lifecycleExecutionRef));
        ApplicationState = applicationState;
    }

    public ProjectIdentityInfo Project { get; }

    public ExecutionRef LifecycleExecutionRef { get; }

    public ExecutionApplicationState ApplicationState { get; }
}
