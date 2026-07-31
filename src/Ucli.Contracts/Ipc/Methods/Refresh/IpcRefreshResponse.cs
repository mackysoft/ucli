using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Projects;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a successful <c>project.refresh</c> Lifecycle Execution response. </summary>
public sealed record IpcRefreshResponse
{
    /// <summary> Initializes one completed refresh response. </summary>
    [JsonConstructor]
    public IpcRefreshResponse (
        UnityProjectIdentity project,
        ExecutionRef lifecycleExecutionRef,
        RefreshLifecycleResult result)
    {
        Project = project ?? throw new ArgumentNullException(nameof(project));
        LifecycleExecutionRef =
            IpcLifecycleExecutionContractGuard.RequireSuccessfulReference(
                lifecycleExecutionRef,
                LifecycleExecutionKind.Refresh,
                nameof(lifecycleExecutionRef));
        Result = result ?? throw new ArgumentNullException(nameof(result));
        if (result.Lifecycle.ProjectFingerprint != project.ProjectFingerprint)
        {
            throw new ArgumentException(
                "Refresh lifecycle observation must match the response project.",
                nameof(result));
        }
    }

    /// <summary> Gets the project identity used by the refresh action. </summary>
    [JsonInclude]
    [JsonRequired]
    public UnityProjectIdentity Project { get; private init; }

    /// <summary> Gets the completed terminal reference of the refresh action. </summary>
    [JsonInclude]
    [JsonRequired]
    public ExecutionRef LifecycleExecutionRef { get; private init; }

    /// <summary> Gets the provider-independent refresh result. </summary>
    [JsonInclude]
    [JsonRequired]
    public RefreshLifecycleResult Result { get; private init; }
}
