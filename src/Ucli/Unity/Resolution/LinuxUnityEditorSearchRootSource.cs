namespace MackySoft.Ucli.UnityProject.Resolution;

/// <summary> Provides Linux search-root candidates for Unity editor installations. </summary>
internal sealed class LinuxUnityEditorSearchRootSource : IUnityEditorSearchRootSource
{
    /// <summary> Gets a value indicating whether this source supports the current platform. </summary>
    public bool IsSupportedCurrentPlatform => OperatingSystem.IsLinux();

    /// <summary> Appends candidate root directories for Unity editor installations on Linux. </summary>
    /// <param name="searchRootBuilder"> The destination builder that stores de-duplicated roots. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="searchRootBuilder" /> is <see langword="null" />. </exception>
    public void AppendSearchRoots (UnityEditorSearchRootBuilder searchRootBuilder)
    {
        ArgumentNullException.ThrowIfNull(searchRootBuilder);

        if (!IsSupportedCurrentPlatform)
        {
            return;
        }
        searchRootBuilder.Add("/opt/Unity/Hub/Editor");
        searchRootBuilder.Add("/opt/unity/hub/editor");

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            searchRootBuilder.Add(Path.Combine(userProfile, "Unity", "Hub", "Editor"));
        }
    }
}