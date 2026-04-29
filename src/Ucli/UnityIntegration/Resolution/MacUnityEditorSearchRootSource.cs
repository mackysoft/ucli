namespace MackySoft.Ucli.UnityIntegration.Resolution;

/// <summary> Provides macOS search-root candidates for Unity editor installations. </summary>
internal sealed class MacUnityEditorSearchRootSource : IUnityEditorSearchRootSource
{
    /// <summary> Gets a value indicating whether this source supports the current platform. </summary>
    public bool IsSupportedCurrentPlatform => OperatingSystem.IsMacOS();

    /// <summary> Appends candidate root directories for Unity editor installations on macOS. </summary>
    /// <param name="searchRootBuilder"> The destination builder that stores de-duplicated roots. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="searchRootBuilder" /> is <see langword="null" />. </exception>
    public void AppendSearchRoots (UnityEditorSearchRootBuilder searchRootBuilder)
    {
        ArgumentNullException.ThrowIfNull(searchRootBuilder);

        if (!IsSupportedCurrentPlatform)
        {
            return;
        }
        searchRootBuilder.Add("/Applications/Unity/Hub/Editor");
        searchRootBuilder.Add("/Applications/Unity/Editor");

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            searchRootBuilder.Add(Path.Combine(userProfile, "Unity", "Hub", "Editor"));
        }
    }
}
