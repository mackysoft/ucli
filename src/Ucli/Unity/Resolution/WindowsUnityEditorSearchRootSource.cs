namespace MackySoft.Ucli.UnityProject.Resolution;

/// <summary> Provides Windows search-root candidates for Unity editor installations. </summary>
internal sealed class WindowsUnityEditorSearchRootSource : IUnityEditorSearchRootSource
{
    /// <summary> Gets a value indicating whether this source supports the current platform. </summary>
    public bool IsSupportedCurrentPlatform => OperatingSystem.IsWindows();

    /// <summary> Gets candidate root directories for Unity editor installations on Windows. </summary>
    /// <returns> Candidate root directories. </returns>
    public IReadOnlyList<string> GetSearchRoots ()
    {
        if (!IsSupportedCurrentPlatform)
        {
            return Array.Empty<string>();
        }

        var roots = new List<string>();

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrWhiteSpace(programFiles))
        {
            roots.Add(Path.Combine(programFiles, "Unity", "Hub", "Editor"));
            roots.Add(Path.Combine(programFiles, "Unity", "Editor"));
        }

        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrWhiteSpace(programFilesX86))
        {
            roots.Add(Path.Combine(programFilesX86, "Unity", "Hub", "Editor"));
            roots.Add(Path.Combine(programFilesX86, "Unity", "Editor"));
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            roots.Add(Path.Combine(userProfile, "Unity", "Hub", "Editor"));
        }

        return roots;
    }
}