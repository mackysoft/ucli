namespace MackySoft.Ucli.Contracts.Recording;

/// <summary>Defines Recorder-independent compatibility metadata shipped with the uCLI Recorder adapter.</summary>
public static class GameViewRecorderCompatibilityMetadata
{
    /// <summary>Gets the Unity Recorder package identifier.</summary>
    public const string PackageId = "com.unity.recorder";

    /// <summary>Gets the supported Unity Recorder package version range.</summary>
    public const string RecorderPackageVersionRange = "[5.1.5,5.2.0)";

    /// <summary>Gets the stable uCLI GameView Recorder adapter identifier.</summary>
    public const string AdapterId = "com.mackysoft.ucli.game-view-recorder";

    /// <summary>Gets the immutable adapter compatibility-table version.</summary>
    public const string AdapterVersion = "1";
}
