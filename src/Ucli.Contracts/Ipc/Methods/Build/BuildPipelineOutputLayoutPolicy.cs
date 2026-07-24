using System.Diagnostics.CodeAnalysis;
using MackySoft.Ucli.Contracts.Assurance.Build;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Resolves command-owned portable BuildPipeline output layouts from stable build target names. </summary>
internal static class BuildPipelineOutputLayoutPolicy
{
    private const string PlayerFileName = "Player";
    private const string PlayerAppBundleName = "Player.app";
    private const string WindowsPlayerFileName = "Player.exe";
    private const string AndroidPlayerFileName = "Player.apk";
    private const string AndroidPlayerAppBundleFileName = "Player.aab";

    /// <summary> Tries to resolve the portable BuildPipeline output layout for the target. </summary>
    /// <param name="buildTarget"> The uCLI build target stable name. </param>
    /// <param name="androidAppBundle"> <see langword="true" /> when the Android output is an App Bundle. Ignored for non-Android targets. </param>
    /// <param name="layout"> The resolved output layout when successful. </param>
    /// <returns> <see langword="true" /> when the target has a deterministic output layout; otherwise <see langword="false" />. </returns>
    public static bool TryResolve (
        BuildTargetStableName buildTarget,
        bool androidAppBundle,
        [NotNullWhen(true)] out BuildPipelineOutputLayoutDefinition? layout)
    {
        layout = null;
        if (!TryResolveShapeAndFileName(buildTarget, androidAppBundle, out var shape, out var fileName))
        {
            return false;
        }

        layout = new BuildPipelineOutputLayoutDefinition(
            shape,
            new BuildRunnerOutputPath($"player/{fileName}"));
        return true;
    }

    private static bool TryResolveShapeAndFileName (
        BuildTargetStableName buildTarget,
        bool androidAppBundle,
        out IpcBuildOutputLayoutShape shape,
        out string fileName)
    {
        switch (buildTarget)
        {
            case BuildTargetStableName.StandaloneOsx:
                shape = IpcBuildOutputLayoutShape.AppBundle;
                fileName = PlayerAppBundleName;
                return true;
            case BuildTargetStableName.StandaloneWindows:
            case BuildTargetStableName.StandaloneWindows64:
                shape = IpcBuildOutputLayoutShape.File;
                fileName = WindowsPlayerFileName;
                return true;
            case BuildTargetStableName.StandaloneLinux64:
                shape = IpcBuildOutputLayoutShape.File;
                fileName = PlayerFileName;
                return true;
            case BuildTargetStableName.Android:
                shape = IpcBuildOutputLayoutShape.File;
                fileName = androidAppBundle ? AndroidPlayerAppBundleFileName : AndroidPlayerFileName;
                return true;
            case BuildTargetStableName.Ios:
            case BuildTargetStableName.Tvos:
            case BuildTargetStableName.Webgl:
                shape = IpcBuildOutputLayoutShape.Directory;
                fileName = PlayerFileName;
                return true;
            default:
                shape = default;
                fileName = null!;
                return false;
        }
    }
}
