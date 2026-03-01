namespace MackySoft.Ucli.UnityProject.Resolution;

/// <summary> Provides Windows search-root candidates for Unity editor installations. </summary>
internal sealed class WindowsUnityEditorSearchRootSource : IUnityEditorSearchRootSource
{
    /// <summary> Gets a value indicating whether this source supports the current platform. </summary>
    public bool IsSupportedCurrentPlatform => OperatingSystem.IsWindows();

    /// <summary> Appends candidate root directories for Unity editor installations on Windows. </summary>
    /// <param name="searchRootSet"> The destination set that stores de-duplicated roots. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="searchRootSet" /> is <see langword="null" />. </exception>
    public void AppendSearchRoots (UnityEditorSearchRootSet searchRootSet)
    {
        ArgumentNullException.ThrowIfNull(searchRootSet);

        if (!IsSupportedCurrentPlatform)
        {
            return;
        }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrWhiteSpace(programFiles))
        {
            searchRootSet.Add(Path.Combine(programFiles, "Unity", "Hub", "Editor"));
            searchRootSet.Add(Path.Combine(programFiles, "Unity", "Editor"));
        }

        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrWhiteSpace(programFilesX86))
        {
            searchRootSet.Add(Path.Combine(programFilesX86, "Unity", "Hub", "Editor"));
            searchRootSet.Add(Path.Combine(programFilesX86, "Unity", "Editor"));
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            searchRootSet.Add(Path.Combine(userProfile, "Unity", "Hub", "Editor"));
        }
    }
}