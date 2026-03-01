namespace MackySoft.Ucli.UnityProject.Resolution;

/// <summary> Provides macOS search-root candidates for Unity editor installations. </summary>
internal sealed class MacUnityEditorSearchRootSource : IUnityEditorSearchRootSource
{
    /// <summary> Gets a value indicating whether this source supports the current platform. </summary>
    public bool IsSupportedCurrentPlatform => OperatingSystem.IsMacOS();

    /// <summary> Gets candidate root directories for Unity editor installations on macOS. </summary>
    /// <returns> Candidate root directories. </returns>
    public IReadOnlyList<string> GetSearchRoots ()
    {
        if (!IsSupportedCurrentPlatform)
        {
            return Array.Empty<string>();
        }

        var roots = new List<string>
        {
            "/Applications/Unity/Hub/Editor",
            "/Applications/Unity/Editor",
        };

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            roots.Add(Path.Combine(userProfile, "Unity", "Hub", "Editor"));
        }

        return roots;
    }
}