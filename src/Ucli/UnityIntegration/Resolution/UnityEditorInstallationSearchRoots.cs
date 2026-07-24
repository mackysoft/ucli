using MackySoft.FileSystem;

namespace MackySoft.Ucli.UnityIntegration.Resolution;

/// <summary> Provides Unity editor installation search roots for local environments. </summary>
internal static class UnityEditorInstallationSearchRoots
{
    private static readonly AbsolutePath[] SearchRoots = CreateSearchRoots();

    /// <summary> Gets candidate root directory paths used for editor discovery. </summary>
    /// <returns> Candidate root paths in deterministic order. </returns>
    public static IReadOnlyList<AbsolutePath> GetSearchRoots ()
    {
        return SearchRoots;
    }

    private static AbsolutePath[] CreateSearchRoots ()
    {
        var searchRootBuilder = new UnityEditorSearchRootBuilder();
        if (OperatingSystem.IsWindows())
        {
            AppendWindowsSearchRoots(searchRootBuilder);
        }
        else if (OperatingSystem.IsMacOS())
        {
            AppendMacSearchRoots(searchRootBuilder);
        }
        else if (OperatingSystem.IsLinux())
        {
            AppendLinuxSearchRoots(searchRootBuilder);
        }
        return searchRootBuilder.ToArray();
    }

    private static void AppendWindowsSearchRoots (UnityEditorSearchRootBuilder searchRootBuilder)
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrWhiteSpace(programFiles))
        {
            AddRoot(searchRootBuilder, Path.Combine(programFiles, "Unity", "Hub", "Editor"));
            AddRoot(searchRootBuilder, Path.Combine(programFiles, "Unity", "Editor"));
        }

        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrWhiteSpace(programFilesX86))
        {
            AddRoot(searchRootBuilder, Path.Combine(programFilesX86, "Unity", "Hub", "Editor"));
            AddRoot(searchRootBuilder, Path.Combine(programFilesX86, "Unity", "Editor"));
        }

        AppendUserProfileSearchRoot(searchRootBuilder);
    }

    private static void AppendMacSearchRoots (UnityEditorSearchRootBuilder searchRootBuilder)
    {
        AddRoot(searchRootBuilder, "/Applications/Unity/Hub/Editor");
        AddRoot(searchRootBuilder, "/Applications/Unity/Editor");
        AppendUserProfileSearchRoot(searchRootBuilder);
    }

    private static void AppendLinuxSearchRoots (UnityEditorSearchRootBuilder searchRootBuilder)
    {
        AddRoot(searchRootBuilder, "/opt/Unity/Hub/Editor");
        AddRoot(searchRootBuilder, "/opt/unity/hub/editor");
        AppendUserProfileSearchRoot(searchRootBuilder);
    }

    private static void AppendUserProfileSearchRoot (UnityEditorSearchRootBuilder searchRootBuilder)
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            AddRoot(searchRootBuilder, Path.Combine(userProfile, "Unity", "Hub", "Editor"));
        }
    }

    private static void AddRoot (
        UnityEditorSearchRootBuilder searchRootBuilder,
        string path)
    {
        if (AbsolutePath.TryParse(path, out var absolutePath, out _))
        {
            searchRootBuilder.Add(absolutePath);
        }
    }
}
