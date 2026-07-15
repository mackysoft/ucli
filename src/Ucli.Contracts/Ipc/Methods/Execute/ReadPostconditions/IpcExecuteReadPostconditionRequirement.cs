using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one read-surface requirement that a later read-index artifact must satisfy. </summary>
/// <param name="Surface"> The affected read surface. </param>
/// <param name="MinSafeGeneratedAtUtc"> The inclusive minimum read-index generation time considered safe. </param>
public sealed record IpcExecuteReadPostconditionRequirement
{
    /// <summary> Initializes one read-surface requirement. </summary>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="Surface" /> is not defined by the contract. </exception>
    [JsonConstructor]
    public IpcExecuteReadPostconditionRequirement (
        IpcExecuteReadPostconditionSurface Surface,
        DateTimeOffset MinSafeGeneratedAtUtc)
    {
        if (!ContractLiteralCodec.IsDefined(Surface))
        {
            throw new ArgumentOutOfRangeException(nameof(Surface), Surface, "Read postcondition surface must be specified.");
        }

        this.Surface = Surface;
        this.MinSafeGeneratedAtUtc = ContractArgumentGuard.RequireUtcTimestamp(
            MinSafeGeneratedAtUtc,
            nameof(MinSafeGeneratedAtUtc));
    }

    public IpcExecuteReadPostconditionSurface Surface { get; }

    public DateTimeOffset MinSafeGeneratedAtUtc { get; }

    /// <summary> Gets the optional normalized scene path for scene-scoped requirements. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ScenePath { get; init; }
}
