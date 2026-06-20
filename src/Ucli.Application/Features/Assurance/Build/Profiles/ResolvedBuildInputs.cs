using MackySoft.Ucli.Contracts.Assurance;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;

/// <summary> Represents build inputs resolved from a build profile. </summary>
internal sealed record ResolvedBuildInputs (
    BuildProfileInputsKind Kind,
    ResolvedBuildTarget? BuildTarget,
    ResolvedBuildScenes? Scenes,
    ResolvedBuildOptions? Options,
    string? UnityBuildProfilePath)
{
    /// <summary> Creates resolved explicit build inputs. </summary>
    public static ResolvedBuildInputs Explicit (
        ResolvedBuildTarget buildTarget,
        ResolvedBuildScenes scenes,
        ResolvedBuildOptions options)
    {
        ArgumentNullException.ThrowIfNull(buildTarget);
        ArgumentNullException.ThrowIfNull(scenes);
        ArgumentNullException.ThrowIfNull(options);
        return new ResolvedBuildInputs(
            BuildProfileInputsKind.Explicit,
            buildTarget,
            scenes,
            options,
            null);
    }

    /// <summary> Creates resolved Unity Build Profile asset inputs. </summary>
    public static ResolvedBuildInputs UnityBuildProfile (string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new ResolvedBuildInputs(
            BuildProfileInputsKind.UnityBuildProfile,
            null,
            null,
            null,
            path);
    }

    /// <summary> Gets the resolved explicit build target or throws when the input kind does not provide one. </summary>
    public ResolvedBuildTarget RequireBuildTarget ()
    {
        return BuildTarget ?? throw new InvalidOperationException("Build target is only available for explicit build inputs.");
    }

    /// <summary> Gets the resolved explicit scene input or throws when the input kind does not provide one. </summary>
    public ResolvedBuildScenes RequireScenes ()
    {
        return Scenes ?? throw new InvalidOperationException("Build scenes are only available for explicit build inputs.");
    }

    /// <summary> Gets the resolved explicit build options or throws when the input kind does not provide them. </summary>
    public ResolvedBuildOptions RequireOptions ()
    {
        return Options ?? throw new InvalidOperationException("Build options are only available for explicit build inputs.");
    }

    /// <summary> Gets the Unity Build Profile asset path or throws when the input kind does not provide one. </summary>
    public string RequireUnityBuildProfilePath ()
    {
        return UnityBuildProfilePath ?? throw new InvalidOperationException("Unity Build Profile path is only available for unityBuildProfile inputs.");
    }
}
