using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Projects;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a typed <c>project.refresh</c> failure response after project resolution. </summary>
public sealed record IpcRefreshErrorResponse
{
    /// <summary> Initializes one refresh failure response from confirmed observations. </summary>
    [JsonConstructor]
    public IpcRefreshErrorResponse (
        UnityProjectIdentity project,
        ExecutionRef? lifecycleExecutionRef,
        ExecutionApplicationState applicationState,
        RefreshLifecycleStartEvidence? refresh,
        UnityEditorObservation? observedLifecycle,
        ExecutionReadPostcondition? readPostcondition)
    {
        Project = project ?? throw new ArgumentNullException(nameof(project));
        LifecycleExecutionRef = IpcLifecycleExecutionContractGuard.RequireErrorReference(
            lifecycleExecutionRef,
            LifecycleExecutionKind.Refresh,
            nameof(lifecycleExecutionRef));
        ApplicationState = IpcLifecycleExecutionContractGuard.RequireApplicationState(
            applicationState,
            nameof(applicationState));
        if (applicationState == ExecutionApplicationState.PartiallyApplied)
        {
            throw new ArgumentOutOfRangeException(
                nameof(applicationState),
                applicationState,
                "Refresh does not support a partially applied state.");
        }
        if (lifecycleExecutionRef == null)
        {
            if (applicationState != ExecutionApplicationState.NotApplied)
            {
                throw new ArgumentException(
                    "A refresh failure without a registered execution must be notApplied.",
                    nameof(applicationState));
            }
        }
        if (refresh != null && applicationState == ExecutionApplicationState.NotApplied)
        {
            throw new ArgumentException(
                "Observed refresh start evidence is incompatible with notApplied.",
                nameof(refresh));
        }
        if (observedLifecycle != null
            && observedLifecycle.ProjectFingerprint != project.ProjectFingerprint)
        {
            throw new ArgumentException(
                "Observed refresh lifecycle must match the response project.",
                nameof(observedLifecycle));
        }

        Refresh = refresh;
        ObservedLifecycle = observedLifecycle;
        ReadPostcondition = readPostcondition;
    }

    /// <summary> Gets the resolved project identity. </summary>
    [JsonInclude]
    [JsonRequired]
    public UnityProjectIdentity Project { get; private init; }

    /// <summary> Gets the registered execution reference, or <see langword="null" /> before registration. </summary>
    [JsonInclude]
    [JsonRequired]
    public ExecutionRef? LifecycleExecutionRef { get; private init; }

    /// <summary> Gets the confirmed refresh application state. </summary>
    [JsonInclude]
    [JsonRequired]
    public ExecutionApplicationState ApplicationState { get; private init; }

    /// <summary> Gets observed refresh-start evidence when available. </summary>
    [JsonInclude]
    [JsonRequired]
    public RefreshLifecycleStartEvidence? Refresh { get; private init; }

    /// <summary> Gets the last complete lifecycle observed on the same project and host. </summary>
    [JsonInclude]
    [JsonRequired]
    public UnityEditorObservation? ObservedLifecycle { get; private init; }

    /// <summary> Gets the optional safety requirements for invalidated read surfaces. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ExecutionReadPostcondition? ReadPostcondition { get; }
}
