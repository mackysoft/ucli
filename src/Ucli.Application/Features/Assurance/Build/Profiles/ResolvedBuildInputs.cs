namespace MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;

/// <summary> Represents build inputs resolved from a build profile. </summary>
internal sealed record ResolvedBuildInputs (
    BuildProfileInputsKind Kind,
    ResolvedBuildTarget BuildTarget,
    ResolvedBuildScenes Scenes,
    ResolvedBuildOptions Options);
