using MackySoft.Ucli.Hosting.Cli.Common.Contracts;

namespace MackySoft.Ucli.Hosting.Cli.Skills;

/// <summary> Normalizes legacy public <c>skills</c> option names before ConsoleAppFramework binding. </summary>
internal static class SkillsCommandArgumentNormalizer
{
    private static readonly Dictionary<string, string> LegacyOptionMap = new(StringComparer.Ordinal)
    {
        ["--repositoryRoot"] = "--repository-root",
        ["--repoRoot"] = "--repository-root",
        ["--repo-root"] = "--repository-root",
        ["--targetDir"] = "--target-dir",
        ["--dryRun"] = "--dry-run",
        ["--printDiff"] = "--print-diff",
    };

    /// <summary> Returns command arguments with legacy skills option names rewritten to generated command option names. </summary>
    public static string[] Normalize (string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Length == 0 || !string.Equals(args[0], UcliCommandNames.Skills, StringComparison.Ordinal))
        {
            return args;
        }

        string[]? normalizedArgs = null;
        for (var i = 1; i < args.Length; i++)
        {
            if (LegacyOptionMap.TryGetValue(args[i], out var normalizedOption))
            {
                normalizedArgs ??= args.ToArray();
                normalizedArgs[i] = normalizedOption;
            }
        }

        return normalizedArgs ?? args;
    }
}
