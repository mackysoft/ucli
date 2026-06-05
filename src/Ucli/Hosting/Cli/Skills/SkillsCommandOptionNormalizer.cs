using MackySoft.AgentSkills.Distribution;
using MackySoft.AgentSkills.Hosts.Registration;
using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Infrastructure.Paths;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Hosting.Cli.Skills;

/// <summary> Normalizes common <c>ucli skills</c> command options. </summary>
internal static class SkillsCommandOptionNormalizer
{
    private const string ProjectScopeLiteral = "project";
    private const string UserScopeLiteral = "user";
    private const string DirectoryExportFormatLiteral = "directory";
    private const string ZipExportFormatLiteral = "zip";
    private const string UnityAssetsDirectoryName = "Assets";
    private const string UnityPackagesDirectoryName = "Packages";
    private const string UnityProjectSettingsDirectoryName = "ProjectSettings";

    /// <summary> Normalizes a required host option to its canonical host key. </summary>
    /// <param name="command"> The command name used for error results. </param>
    /// <param name="host"> The raw host option. </param>
    /// <param name="hostAdapters"> The supported host adapter set. </param>
    /// <param name="errorResult"> The emitted error result when normalization fails. </param>
    /// <returns> The canonical host key, or <see langword="null" /> when normalization fails. </returns>
    public static string? NormalizeHost (
        string command,
        string? host,
        SkillHostAdapterSet hostAdapters,
        out CommandResult? errorResult)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentNullException.ThrowIfNull(hostAdapters);

        errorResult = null;
        if (string.IsNullOrWhiteSpace(host))
        {
            errorResult = CommandResult.InvalidArgument(command, "Option '--host' is required.");
            return null;
        }

        var adapterResult = hostAdapters.GetAdapter(host);
        if (!adapterResult.IsSuccess)
        {
            errorResult = SkillsCommandResultFactory.CreateSkillFailure(command, adapterResult.Failure!);
            return null;
        }

        return adapterResult.Value!.Descriptor.HostKey;
    }

    /// <summary> Normalizes a required scope option. </summary>
    /// <param name="command"> The command name used for error results. </param>
    /// <param name="scope"> The raw scope option. </param>
    /// <param name="errorResult"> The emitted error result when normalization fails. </param>
    /// <returns> The normalized scope kind, or <see langword="null" /> when normalization fails. </returns>
    public static SkillScopeKind? NormalizeScope (
        string command,
        string? scope,
        out CommandResult? errorResult)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);

        errorResult = null;
        if (string.IsNullOrWhiteSpace(scope))
        {
            errorResult = CommandResult.InvalidArgument(command, "Option '--scope' is required.");
            return null;
        }

        if (string.Equals(scope, ProjectScopeLiteral, StringComparison.OrdinalIgnoreCase))
        {
            return SkillScopeKind.Project;
        }

        if (string.Equals(scope, UserScopeLiteral, StringComparison.OrdinalIgnoreCase))
        {
            return SkillScopeKind.User;
        }

        errorResult = CommandResult.InvalidArgument(command, $"Unsupported SKILL scope: {scope}. Supported scopes: {ProjectScopeLiteral}, {UserScopeLiteral}.");
        return null;
    }

    /// <summary> Validates repository root usage for a resolved scope. </summary>
    /// <param name="command"> The command name used for error results. </param>
    /// <param name="scope"> The normalized scope. </param>
    /// <param name="repoRoot"> The raw repository root option. </param>
    /// <param name="errorResult"> The emitted error result when normalization fails. </param>
    /// <returns> The full repository root for project scope; otherwise <see langword="null" />. </returns>
    public static string? NormalizeRepositoryRootForScope (
        string command,
        SkillScopeKind scope,
        string? repoRoot,
        out CommandResult? errorResult)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);

        errorResult = null;
        if (scope == SkillScopeKind.User)
        {
            if (!string.IsNullOrWhiteSpace(repoRoot))
            {
                errorResult = CommandResult.InvalidArgument(command, "Option '--repoRoot' is not supported when '--scope user' is used.");
            }

            return null;
        }

        return !string.IsNullOrWhiteSpace(repoRoot)
            ? NormalizeExplicitRepositoryRoot(command, repoRoot, out errorResult)
            : ResolveDefaultRepositoryRoot(command, out errorResult);
    }

    /// <summary> Validates target-directory usage for a resolved scope. </summary>
    /// <param name="command"> The command name used for error results. </param>
    /// <param name="scope"> The normalized scope. </param>
    /// <param name="repositoryRoot"> The resolved repository root. </param>
    /// <param name="targetDir"> The raw target directory option. </param>
    /// <param name="errorResult"> The emitted error result when validation fails. </param>
    /// <returns> <see langword="true" /> when the target directory can be passed to the SKILL installer; otherwise <see langword="false" />. </returns>
    public static bool ValidateTargetDirectoryForScope (
        string command,
        SkillScopeKind scope,
        string? repositoryRoot,
        string? targetDir,
        out CommandResult? errorResult)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);

        errorResult = null;
        if (scope != SkillScopeKind.Project || string.IsNullOrWhiteSpace(targetDir))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(repositoryRoot))
        {
            errorResult = CommandResult.InvalidArgument(command, "Project-scope target directory validation requires a repository root.");
            return false;
        }

        string normalizedRepositoryRoot;
        string normalizedTargetRoot;
        try
        {
            normalizedRepositoryRoot = NormalizeComparisonPath(repositoryRoot);
            normalizedTargetRoot = Path.IsPathFullyQualified(targetDir)
                ? NormalizeComparisonPath(targetDir)
                : NormalizeComparisonPath(Path.Combine(normalizedRepositoryRoot, targetDir));
        }
        catch (Exception ex) when (PathFormatExceptionClassifier.IsPathFormatException(ex))
        {
            errorResult = CommandResult.InvalidArgument(command, $"Option '--targetDir' is invalid: {ex.Message}");
            return false;
        }

        if (IsSamePath(normalizedTargetRoot, normalizedRepositoryRoot))
        {
            errorResult = CommandResult.InvalidArgument(command, "Option '--targetDir' must point to a SKILL target directory, not the repository root.");
            return false;
        }

        var gitMarkerPath = Path.Combine(normalizedRepositoryRoot, UcliStoragePathNames.GitMarkerName);
        if (IsSameOrUnderPath(normalizedTargetRoot, gitMarkerPath))
        {
            errorResult = CommandResult.InvalidArgument(command, "Option '--targetDir' must not point inside the Git metadata directory.");
            return false;
        }

        var ucliDirectoryPath = Path.Combine(normalizedRepositoryRoot, UcliStoragePathNames.UcliDirectoryName);
        if (IsSameOrUnderPath(normalizedTargetRoot, ucliDirectoryPath))
        {
            errorResult = CommandResult.InvalidArgument(command, "Option '--targetDir' must not point inside the uCLI storage directory.");
            return false;
        }

        if (Directory.Exists(normalizedTargetRoot) && IsUnityProjectRoot(normalizedTargetRoot))
        {
            errorResult = CommandResult.InvalidArgument(command, "Option '--targetDir' must point to a SKILL target directory, not a Unity project root.");
            return false;
        }

        return true;
    }

    /// <summary> Normalizes the optional export format. </summary>
    /// <param name="command"> The command name used for error results. </param>
    /// <param name="format"> The raw format option. </param>
    /// <param name="errorResult"> The emitted error result when normalization fails. </param>
    /// <returns> The normalized export format, or <see langword="null" /> when normalization fails. </returns>
    public static SkillExportFormat? NormalizeExportFormat (
        string command,
        string? format,
        out CommandResult? errorResult)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);

        errorResult = null;
        if (string.IsNullOrWhiteSpace(format) || string.Equals(format, DirectoryExportFormatLiteral, StringComparison.OrdinalIgnoreCase))
        {
            return SkillExportFormat.Directory;
        }

        if (string.Equals(format, ZipExportFormatLiteral, StringComparison.OrdinalIgnoreCase))
        {
            return SkillExportFormat.Zip;
        }

        errorResult = CommandResult.InvalidArgument(command, $"Unsupported SKILL export format: {format}. Supported formats: {DirectoryExportFormatLiteral}, {ZipExportFormatLiteral}.");
        return null;
    }

    /// <summary> Normalizes a required filesystem path option to a full path. </summary>
    /// <param name="command"> The command name used for error results. </param>
    /// <param name="optionName"> The option name used in error messages. </param>
    /// <param name="value"> The raw option value. </param>
    /// <param name="errorResult"> The emitted error result when normalization fails. </param>
    /// <returns> The full path, or <see langword="null" /> when normalization fails. </returns>
    public static string? NormalizeRequiredFullPath (
        string command,
        string optionName,
        string? value,
        out CommandResult? errorResult)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(optionName);

        errorResult = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            errorResult = CommandResult.InvalidArgument(command, $"Option '--{optionName}' is required.");
            return null;
        }

        try
        {
            return Path.GetFullPath(value);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            errorResult = CommandResult.InvalidArgument(command, $"Option '--{optionName}' is invalid: {ex.Message}");
            return null;
        }
    }

    private static string? NormalizeExplicitRepositoryRoot (
        string command,
        string repoRoot,
        out CommandResult? errorResult)
    {
        var normalizedRepositoryRoot = NormalizeRequiredFullPath(command, "repoRoot", repoRoot, out errorResult);
        if (errorResult is not null)
        {
            return null;
        }

        return ValidateRepositoryRoot(command, normalizedRepositoryRoot!, "Option '--repoRoot'", out errorResult);
    }

    private static string? ResolveDefaultRepositoryRoot (
        string command,
        out CommandResult? errorResult)
    {
        errorResult = null;
        try
        {
            var currentDirectoryPath = Path.GetFullPath(Environment.CurrentDirectory);
            var repositoryRoot = UcliStoragePathResolver.TryResolveRepositoryRoot(currentDirectoryPath);
            if (repositoryRoot is not null)
            {
                return repositoryRoot;
            }

            errorResult = CommandResult.InvalidArgument(
                command,
                "Could not resolve a Git repository root from the current working directory. Run the command inside a Git repository or specify --repoRoot.");
            return null;
        }
        catch (Exception ex) when (PathFormatExceptionClassifier.IsPathFormatException(ex))
        {
            errorResult = CommandResult.InvalidArgument(
                command,
                $"Current working directory path is invalid: {Environment.CurrentDirectory}. {ex.Message}");
            return null;
        }
    }

    private static string? ValidateRepositoryRoot (
        string command,
        string repositoryRoot,
        string sourceName,
        out CommandResult? errorResult)
    {
        errorResult = null;
        if (!Directory.Exists(repositoryRoot))
        {
            errorResult = CommandResult.InvalidArgument(command, $"{sourceName} must point to an existing directory: {repositoryRoot}");
            return null;
        }

        string? resolvedRepositoryRoot;
        try
        {
            resolvedRepositoryRoot = UcliStoragePathResolver.TryResolveRepositoryRoot(repositoryRoot);
        }
        catch (Exception ex) when (PathFormatExceptionClassifier.IsPathFormatException(ex))
        {
            errorResult = CommandResult.InvalidArgument(command, $"{sourceName} is invalid: {ex.Message}");
            return null;
        }

        if (resolvedRepositoryRoot is null)
        {
            errorResult = CommandResult.InvalidArgument(command, $"{sourceName} must point to a Git repository root: {repositoryRoot}");
            return null;
        }

        if (!IsSamePath(resolvedRepositoryRoot, repositoryRoot))
        {
            errorResult = CommandResult.InvalidArgument(
                command,
                $"{sourceName} must point to the Git repository root, not a subdirectory. Resolved repository root: {resolvedRepositoryRoot}");
            return null;
        }

        return resolvedRepositoryRoot;
    }

    private static bool IsUnityProjectRoot (string directoryPath)
    {
        return Directory.Exists(Path.Combine(directoryPath, UnityAssetsDirectoryName))
            && Directory.Exists(Path.Combine(directoryPath, UnityPackagesDirectoryName))
            && Directory.Exists(Path.Combine(directoryPath, UnityProjectSettingsDirectoryName));
    }

    private static bool IsSameOrUnderPath (
        string path,
        string parentPath)
    {
        if (IsSamePath(path, parentPath))
        {
            return true;
        }

        var normalizedParent = NormalizeComparisonPath(parentPath);
        var parentPrefix = Path.EndsInDirectorySeparator(normalizedParent)
            ? normalizedParent
            : normalizedParent + Path.DirectorySeparatorChar;
        return NormalizeComparisonPath(path).StartsWith(parentPrefix, PathComparison);
    }

    private static bool IsSamePath (
        string left,
        string right)
    {
        return string.Equals(NormalizeComparisonPath(left), NormalizeComparisonPath(right), PathComparison);
    }

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static string NormalizeComparisonPath (string path)
    {
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }
}
