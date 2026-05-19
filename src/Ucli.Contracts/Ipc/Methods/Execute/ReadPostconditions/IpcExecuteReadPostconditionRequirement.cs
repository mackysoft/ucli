using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one read-surface requirement that a later read-index artifact must satisfy. </summary>
/// <param name="Surface"> The affected read surface. </param>
/// <param name="MinSafeGeneratedAtUtc"> The inclusive minimum read-index generation time considered safe. </param>
public sealed record IpcExecuteReadPostconditionRequirement (
    string Surface,
    DateTimeOffset MinSafeGeneratedAtUtc)
{
    /// <summary> Gets the optional normalized scene path for scene-scoped requirements. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ScenePath { get; init; }
}
