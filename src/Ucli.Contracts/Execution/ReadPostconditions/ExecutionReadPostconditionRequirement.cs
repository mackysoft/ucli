using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Contracts.Projects;

namespace MackySoft.Ucli.Contracts.Execution;

/// <summary> Represents one read-surface requirement that a later read-index artifact must satisfy. </summary>
/// <param name="Surface"> The affected read surface. </param>
/// <param name="MinSafeGeneratedAtUtc"> The inclusive minimum read-index generation time considered safe. </param>
/// <param name="ScenePath"> The scene path for a scene-scoped requirement; otherwise <see langword="null" />. </param>
public sealed record ExecutionReadPostconditionRequirement
{
    /// <summary> Initializes one read-surface requirement. </summary>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="MinSafeGeneratedAtUtc" /> is not a UTC timestamp, or when <paramref name="ScenePath" /> is specified for a project-scoped surface.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="Surface" /> is not defined by the contract. </exception>
    [JsonConstructor]
    public ExecutionReadPostconditionRequirement (
        ExecutionReadPostconditionSurface Surface,
        DateTimeOffset MinSafeGeneratedAtUtc,
        UnityScenePath? ScenePath)
    {
        if (!TextVocabulary.IsDefined(Surface))
        {
            throw new ArgumentOutOfRangeException(nameof(Surface), Surface, "Read postcondition surface must be specified.");
        }

        if (Surface is ExecutionReadPostconditionSurface.AssetSearch or ExecutionReadPostconditionSurface.GuidPath
            && ScenePath is not null)
        {
            throw new ArgumentException("Scene path must be omitted for project-scoped read postconditions.", nameof(ScenePath));
        }

        this.Surface = Surface;
        this.MinSafeGeneratedAtUtc = ContractArgumentGuard.RequireUtcTimestamp(
            MinSafeGeneratedAtUtc,
            nameof(MinSafeGeneratedAtUtc));
        this.ScenePath = ScenePath;
    }

    public ExecutionReadPostconditionSurface Surface { get; }

    public DateTimeOffset MinSafeGeneratedAtUtc { get; }

    /// <summary> Gets the optional normalized scene path for scene-scoped requirements. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UnityScenePath? ScenePath { get; }
}
