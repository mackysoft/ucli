using MackySoft.Ucli.Infrastructure.Paths;

namespace MackySoft.Ucli.UnityIntegration.Resolution;

/// <summary> Provides Unity editor installation search roots for local environments. </summary>
internal static class UnityEditorInstallationSearchRoots
{
    private static readonly string[] SearchRoots = CreateSearchRoots();

    /// <summary> Gets candidate root directory paths used for editor discovery. </summary>
    /// <returns> Candidate root paths in deterministic order. </returns>
    public static IReadOnlyList<string> GetSearchRoots ()
    {
        return SearchRoots;
    }

    private static string[] CreateSearchRoots ()
    {
        var searchRootBuilder = new UnityEditorSearchRootBuilder(PathStringNormalizer.CurrentPlatformPathComparer);
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
            searchRootBuilder.Add(Path.Combine(programFiles, "Unity", "Hub", "Editor"));
            searchRootBuilder.Add(Path.Combine(programFiles, "Unity", "Editor"));
        }

        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrWhiteSpace(programFilesX86))
        {
            searchRootBuilder.Add(Path.Combine(programFilesX86, "Unity", "Hub", "Editor"));
            searchRootBuilder.Add(Path.Combine(programFilesX86, "Unity", "Editor"));
        }

        AppendUserProfileSearchRoot(searchRootBuilder);
    }

    private static void AppendMacSearchRoots (UnityEditorSearchRootBuilder searchRootBuilder)
    {
        searchRootBuilder.Add("/Applications/Unity/Hub/Editor");
        searchRootBuilder.Add("/Applications/Unity/Editor");
        AppendUserProfileSearchRoot(searchRootBuilder);
    }

    private static void AppendLinuxSearchRoots (UnityEditorSearchRootBuilder searchRootBuilder)
    {
        searchRootBuilder.Add("/opt/Unity/Hub/Editor");
        searchRootBuilder.Add("/opt/unity/hub/editor");
        AppendUserProfileSearchRoot(searchRootBuilder);
    }

    private static void AppendUserProfileSearchRoot (UnityEditorSearchRootBuilder searchRootBuilder)
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            searchRootBuilder.Add(Path.Combine(userProfile, "Unity", "Hub", "Editor"));
        }
    }
}
