namespace MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;

/// <summary> Represents one resolved build profile consumed by build execution. </summary>
internal sealed record ResolvedBuildProfile (
    int SchemaVersion,
    ResolvedBuildInputs Inputs,
    ResolvedBuildRunner Runner,
    ResolvedBuildPolicy Policy,
    string Digest)
{
    /// <summary> Gets the resolved build target used by the current BuildPipeline runner. </summary>
    public ResolvedBuildTarget BuildTarget => Inputs.BuildTarget;

    /// <summary> Gets the resolved build scenes used by the current BuildPipeline runner. </summary>
    public ResolvedBuildScenes Scenes => Inputs.Scenes;

    /// <summary> Gets the resolved build options used by the current BuildPipeline runner. </summary>
    public ResolvedBuildOptions Options => Inputs.Options;

    /// <summary> Gets the build output policy currently owned by the build run contract. </summary>
    public ResolvedBuildOutputPolicy Output { get; } = new(BuildProfileOutputKind.UcliArtifact);
}
