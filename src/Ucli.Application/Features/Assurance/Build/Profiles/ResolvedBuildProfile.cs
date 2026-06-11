namespace MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;

/// <summary> Represents one resolved build profile consumed by build execution. </summary>
internal sealed record ResolvedBuildProfile (
    int SchemaVersion,
    ResolvedBuildTarget Target,
    ResolvedBuildScenes Scenes,
    ResolvedBuildOutputPolicy Output,
    ResolvedBuildOptions Options,
    string Digest);
