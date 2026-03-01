namespace MackySoft.Ucli.UnityProject.Resolution;

/// <summary> Provides macOS search-root candidates for Unity editor installations. </summary>
internal sealed class MacUnityEditorSearchRootSource : IUnityEditorSearchRootSource
{
    /// <summary> Gets a value indicating whether this source supports the current platform. </summary>
    public bool IsSupportedCurrentPlatform => OperatingSystem.IsMacOS();

    /// <summary> Appends candidate root directories for Unity editor installations on macOS. </summary>
    /// <param name="searchRootSet"> The destination set that stores de-duplicated roots. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="searchRootSet" /> is <see langword="null" />. </exception>
    public void AppendSearchRoots (UnityEditorSearchRootSet searchRootSet)
    {
        ArgumentNullException.ThrowIfNull(searchRootSet);

        if (!IsSupportedCurrentPlatform)
        {
            return;
        }
        searchRootSet.Add("/Applications/Unity/Hub/Editor");
        searchRootSet.Add("/Applications/Unity/Editor");

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            searchRootSet.Add(Path.Combine(userProfile, "Unity", "Hub", "Editor"));
        }
    }
}