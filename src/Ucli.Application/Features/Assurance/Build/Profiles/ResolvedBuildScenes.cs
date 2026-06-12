namespace MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;

/// <summary> Represents build scene selection resolved from a build profile. </summary>
internal sealed record ResolvedBuildScenes (
    BuildProfileSceneSource Source,
    IReadOnlyList<string> Paths);
