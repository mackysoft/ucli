using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.Application.Features.Requests.Refresh.UseCases.Refresh;

/// <summary> Represents confirmed refresh facts retained when a command does not complete successfully. </summary>
internal sealed record RefreshExecutionErrorOutput (
    ProjectIdentityInfo Project,
    Guid RequestId,
    ExecutionRef? LifecycleExecutionRef,
    ExecutionApplicationState ApplicationState,
    RefreshLifecycleStartEvidence? Refresh,
    UnityEditorObservation? ObservedLifecycle,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    ExecutionReadPostcondition? ReadPostcondition)
{
    public ExecutionApplicationState ApplicationState { get; } =
        RequireApplicationState(ApplicationState);

    private static ExecutionApplicationState RequireApplicationState (
        ExecutionApplicationState applicationState)
    {
        if (!TextVocabulary.IsDefined(applicationState))
        {
            throw new ArgumentOutOfRangeException(
                nameof(applicationState),
                applicationState,
                "Refresh application state must be defined.");
        }
        if (applicationState == ExecutionApplicationState.PartiallyApplied)
        {
            throw new ArgumentOutOfRangeException(
                nameof(applicationState),
                applicationState,
                "Refresh does not support a partially applied state.");
        }

        return applicationState;
    }
}
