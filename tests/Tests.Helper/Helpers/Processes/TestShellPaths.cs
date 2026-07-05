namespace MackySoft.Tests;

internal static class TestShellPaths
{
    public static string QuoteBashArgument (string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return "'" + value.Replace("'", "'\"'\"'", StringComparison.Ordinal) + "'";
    }

    public static string ToBashPath (string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string fullPath = Path.GetFullPath(path).Replace('\\', '/');
        if (!OperatingSystem.IsWindows())
        {
            return fullPath;
        }

        if (fullPath.Length >= 2 && fullPath[1] == ':')
        {
            char driveLetter = char.ToLowerInvariant(fullPath[0]);
            return "/" + driveLetter + fullPath[2..];
        }

        return fullPath;
    }

    public static string ResolveBashFileName ()
    {
        if (!OperatingSystem.IsWindows())
        {
            return "bash";
        }

        foreach (string candidatePath in EnumerateGitBashCandidatePaths())
        {
            if (File.Exists(candidatePath))
            {
                return candidatePath;
            }
        }

        return "bash";
    }

    private static IEnumerable<string> EnumerateGitBashCandidatePaths ()
    {
        var visitedRootPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string?[] rootPaths =
        [
            Environment.GetEnvironmentVariable("ProgramFiles"),
            Environment.GetEnvironmentVariable("ProgramW6432"),
            Environment.GetEnvironmentVariable("ProgramFiles(x86)"),
        ];

        foreach (string? rootPath in rootPaths)
        {
            if (string.IsNullOrWhiteSpace(rootPath) || !visitedRootPaths.Add(rootPath))
            {
                continue;
            }

            yield return Path.Combine(rootPath, "Git", "bin", "bash.exe");
            yield return Path.Combine(rootPath, "Git", "usr", "bin", "bash.exe");
        }
    }
}
