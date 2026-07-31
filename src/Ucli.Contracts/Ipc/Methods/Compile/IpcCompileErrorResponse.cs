using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a typed <c>compile</c> Lifecycle Execution failure response. </summary>
public sealed record IpcCompileErrorResponse
{
    /// <summary> Initializes one compile failure response from confirmed observations. </summary>
    [JsonConstructor]
    public IpcCompileErrorResponse (
        ExecutionRef? lifecycleExecutionRef,
        ExecutionApplicationState applicationState,
        CompileLifecycleResult? result,
        UnityEditorObservation? observedLifecycle)
    {
        LifecycleExecutionRef = IpcLifecycleExecutionContractGuard.RequireErrorReference(
            lifecycleExecutionRef,
            LifecycleExecutionKind.Compile,
            nameof(lifecycleExecutionRef));
        ApplicationState = IpcLifecycleExecutionContractGuard.RequireApplicationState(
            applicationState,
            nameof(applicationState));
        if (lifecycleExecutionRef == null)
        {
            if (applicationState != ExecutionApplicationState.NotApplied)
            {
                throw new ArgumentException(
                    "A compile failure without a registered execution must be notApplied.",
                    nameof(applicationState));
            }
            if (result != null)
            {
                throw new ArgumentException(
                    "A compile result cannot exist before execution registration.",
                    nameof(result));
            }
        }

        Result = result;
        ObservedLifecycle = observedLifecycle;
    }

    /// <summary> Gets the registered execution reference, or <see langword="null" /> before registration. </summary>
    [JsonInclude]
    [JsonRequired]
    public ExecutionRef? LifecycleExecutionRef { get; private init; }

    /// <summary> Gets the confirmed compile application state. </summary>
    [JsonInclude]
    [JsonRequired]
    public ExecutionApplicationState ApplicationState { get; private init; }

    /// <summary> Gets the typed partial or terminal compile result when available. </summary>
    [JsonInclude]
    [JsonRequired]
    public CompileLifecycleResult? Result { get; private init; }

    /// <summary> Gets the last complete lifecycle observed on the same host. </summary>
    [JsonInclude]
    [JsonRequired]
    public UnityEditorObservation? ObservedLifecycle { get; private init; }
}
