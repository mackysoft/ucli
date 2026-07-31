using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a successful <c>compile</c> Lifecycle Execution response. </summary>
public sealed record IpcCompileResponse
{
    /// <summary> Initializes one completed compile response. </summary>
    [JsonConstructor]
    public IpcCompileResponse (
        ExecutionRef lifecycleExecutionRef,
        CompileLifecycleResult result)
    {
        LifecycleExecutionRef =
            IpcLifecycleExecutionContractGuard.RequireSuccessfulReference(
                lifecycleExecutionRef,
                LifecycleExecutionKind.Compile,
                nameof(lifecycleExecutionRef));
        Result = result ?? throw new ArgumentNullException(nameof(result));
    }

    /// <summary> Gets the completed terminal reference of the compile action. </summary>
    [JsonInclude]
    [JsonRequired]
    public ExecutionRef LifecycleExecutionRef { get; private init; }

    /// <summary> Gets the provider-independent compile result. </summary>
    [JsonInclude]
    [JsonRequired]
    public CompileLifecycleResult Result { get; private init; }
}
