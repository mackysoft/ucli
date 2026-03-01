namespace MackySoft.Ucli.UnityProject.Resolution;

/// <summary> Provides default Unity editor installation roots for local environments. </summary>
internal sealed class DefaultUnityEditorSearchRootProvider : IUnityEditorSearchRootProvider
{
    /// <summary> Gets candidate root directory paths used for editor discovery. </summary>
    /// <returns> Candidate root paths in deterministic order. </returns>
    public IReadOnlyList<string> GetSearchRoots ()
    {
        var roots = new List<string>
        {
            "/Applications/Unity/Hub/Editor",
            "/Applications/Unity/Editor",
            "/opt/Unity/Hub/Editor",
            "/opt/unity/hub/editor",
        };

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

        // NOTE:
        // Linux/macOS are case-sensitive file systems by default.
        // Keep case-variant roots as distinct candidates outside Windows.
        var distinctComparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

        return roots
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Distinct(distinctComparer)
            .ToArray();
    }
}