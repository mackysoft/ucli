using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Resolves command-owned BuildPipeline output layouts from stable build target names. </summary>
internal static class IpcBuildOutputLayoutResolver
{
    private const string PlayerDirectoryName = "player";
    private const string PlayerFileName = "Player";
    private const string PlayerAppBundleName = "Player.app";
    private const string WindowsPlayerFileName = "Player.exe";
    private const string AndroidPlayerFileName = "Player.apk";
    private const string AndroidPlayerAppBundleFileName = "Player.aab";

    /// <summary> Tries to resolve the BuildPipeline output layout for the target. </summary>
    /// <param name="outputDirectory"> The absolute runner working output root. </param>
    /// <param name="buildTarget"> The uCLI build target stable name. </param>
    /// <param name="layout"> The resolved output layout when successful. </param>
    /// <returns> <see langword="true" /> when the target has a deterministic output layout; otherwise <see langword="false" />. </returns>
    public static bool TryResolve (
        string outputDirectory,
        string buildTarget,
        out IpcBuildOutputLayout? layout)
    {
        return TryResolve(outputDirectory, buildTarget, androidAppBundle: false, out layout);
    }

    /// <summary> Tries to resolve the BuildPipeline output layout for the target. </summary>
    /// <param name="outputDirectory"> The absolute runner working output root. </param>
    /// <param name="buildTarget"> The uCLI build target stable name. </param>
    /// <param name="androidAppBundle"> <see langword="true" /> when the Android output is an App Bundle. Ignored for non-Android targets. </param>
    /// <param name="layout"> The resolved output layout when successful. </param>
    /// <returns> <see langword="true" /> when the target has a deterministic output layout; otherwise <see langword="false" />. </returns>
    public static bool TryResolve (
        string outputDirectory,
        string buildTarget,
        bool androidAppBundle,
        out IpcBuildOutputLayout? layout)
    {
        layout = null;
        if (string.IsNullOrWhiteSpace(outputDirectory) || string.IsNullOrWhiteSpace(buildTarget))
        {
            return false;
        }

        if (!ContractLiteralCodec.TryParse<BuildTargetStableName>(buildTarget, out var stableName)
            || !TryResolveShapeAndFileName(stableName, androidAppBundle, out var shape, out var fileName))
        {
            return false;
        }

        layout = new IpcBuildOutputLayout(
            Shape: shape,
            LocationPathName: CombineProtocolPath(outputDirectory, PlayerDirectoryName, fileName));
        return true;
    }

    private static string CombineProtocolPath (string root, string firstSegment, string secondSegment)
    {
        return string.Concat(TrimTrailingDirectorySeparators(root), "/", firstSegment, "/", secondSegment);
    }

    private static string TrimTrailingDirectorySeparators (string value)
    {
        var length = value.Length;
        while (length > 0 && IsDirectorySeparator(value[length - 1]))
        {
            length--;
        }

        return length == value.Length ? value : value[..length];
    }

    private static bool IsDirectorySeparator (char value)
    {
        return value == '/' || value == '\\';
    }

    private static bool TryResolveShapeAndFileName (
        BuildTargetStableName buildTarget,
        bool androidAppBundle,
        out string shape,
        out string fileName)
    {
        switch (buildTarget)
        {
            case BuildTargetStableName.StandaloneOsx:
                shape = ContractLiteralCodec.ToValue(IpcBuildOutputLayoutShape.AppBundle);
                fileName = PlayerAppBundleName;
                return true;
            case BuildTargetStableName.StandaloneWindows:
            case BuildTargetStableName.StandaloneWindows64:
                shape = ContractLiteralCodec.ToValue(IpcBuildOutputLayoutShape.File);
                fileName = WindowsPlayerFileName;
                return true;
            case BuildTargetStableName.StandaloneLinux64:
                shape = ContractLiteralCodec.ToValue(IpcBuildOutputLayoutShape.File);
                fileName = PlayerFileName;
                return true;
            case BuildTargetStableName.Android:
                shape = ContractLiteralCodec.ToValue(IpcBuildOutputLayoutShape.File);
                fileName = androidAppBundle ? AndroidPlayerAppBundleFileName : AndroidPlayerFileName;
                return true;
            case BuildTargetStableName.Ios:
            case BuildTargetStableName.Tvos:
            case BuildTargetStableName.Webgl:
                shape = ContractLiteralCodec.ToValue(IpcBuildOutputLayoutShape.Directory);
                fileName = PlayerFileName;
                return true;
            default:
                shape = null!;
                fileName = null!;
                return false;
        }
    }
}
