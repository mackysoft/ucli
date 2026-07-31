using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a successful Play Mode Lifecycle Execution response. </summary>
public sealed record IpcPlayTransitionResponse
{
    /// <summary> Initializes one completed Play Mode transition response. </summary>
    [JsonConstructor]
    public IpcPlayTransitionResponse (
        ExecutionRef lifecycleExecutionRef,
        PlayLifecycleTransitionResult result)
    {
        Result = result ?? throw new ArgumentNullException(nameof(result));
        if (!result.IsSuccessful)
        {
            throw new ArgumentException(
                "Successful Play Mode response requires a completed transition result.",
                nameof(result));
        }

        var expectedKind = result.Transition == PlayLifecycleTransitionCommand.Enter
            ? LifecycleExecutionKind.PlayEnter
            : LifecycleExecutionKind.PlayExit;
        LifecycleExecutionRef =
            IpcLifecycleExecutionContractGuard.RequireSuccessfulReference(
                lifecycleExecutionRef,
                expectedKind,
                nameof(lifecycleExecutionRef));
    }

    /// <summary> Gets the completed terminal reference of the Play Mode action. </summary>
    [JsonInclude]
    [JsonRequired]
    public ExecutionRef LifecycleExecutionRef { get; private init; }

    /// <summary> Gets the provider-independent Play Mode transition result. </summary>
    [JsonInclude]
    [JsonRequired]
    public PlayLifecycleTransitionResult Result { get; private init; }
}
