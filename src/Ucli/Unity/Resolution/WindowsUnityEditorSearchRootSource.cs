namespace MackySoft.Ucli.UnityProject.Resolution;

/// <summary> Provides Windows search-root candidates for Unity editor installations. </summary>
internal sealed class WindowsUnityEditorSearchRootSource : IUnityEditorSearchRootSource
{
    /// <summary> Gets a value indicating whether this source supports the current platform. </summary>
    public bool IsSupportedCurrentPlatform => OperatingSystem.IsWindows();

    /// <summary> Appends candidate root directories for Unity editor installations on Windows. </summary>
    /// <param name="searchRootBuilder"> The destination builder that stores de-duplicated roots. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="searchRootBuilder" /> is <see langword="null" />. </exception>
    public void AppendSearchRoots (UnityEditorSearchRootBuilder searchRootBuilder)
    {
        ArgumentNullException.ThrowIfNull(searchRootBuilder);

        if (!IsSupportedCurrentPlatform)
        {
            return;
        }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrWhiteSpace(programFiles))
        {
            searchRootBuilder.Add(Path.Combine(programFiles, "Unity", "Hub", "Editor"));
            searchRootBuilder.Add(Path.Combine(programFiles, "Unity", "Editor"));
        }

        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrWhiteSpace(programFilesX86))
        {
            searchRootBuilder.Add(Path.Combine(programFilesX86, "Unity", "Hub", "Editor"));
            searchRootBuilder.Add(Path.Combine(programFilesX86, "Unity", "Editor"));
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            searchRootBuilder.Add(Path.Combine(userProfile, "Unity", "Hub", "Editor"));
        }
    }
}