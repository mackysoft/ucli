using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Shared.Execution.Results;

/// <summary> Represents one read-surface requirement produced by a mutation operation. </summary>
/// <param name="Surface"> The affected read surface. </param>
/// <param name="MinSafeGeneratedAtUtc"> The inclusive minimum read-index generation time considered safe. </param>
internal sealed record OperationExecutionReadPostconditionRequirement (
    IpcExecuteReadPostconditionSurface Surface,
    DateTimeOffset MinSafeGeneratedAtUtc)
{
    /// <summary> Gets the optional normalized scene path for scene-scoped requirements. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ScenePath { get; init; }
}
