using MackySoft.AgentSkills.Distribution;
using MackySoft.AgentSkills.Hosts.Registration;
using MackySoft.AgentSkills.Installation.Targeting;
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

    /// <summary> Normalizes the required tier option. </summary>
    /// <param name="command"> The command name used for error results. </param>
    /// <param name="tiers"> The raw tier options. </param>
    /// <param name="errorResult"> The emitted error result when normalization fails. </param>
    /// <returns> The normalized tier selection, or <see langword="null" /> when normalization fails. </returns>
    public static IReadOnlyList<MackySoft.AgentSkills.Tiers.SkillTier>? NormalizeTiers (
        string command,
        string[]? tiers,
        out CommandResult? errorResult)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);

        errorResult = null;
        if (tiers == null || tiers.Length == 0)
        {
            errorResult = CommandResult.InvalidArgument(command, "Option '--tier' is required.");
            return null;
        }

        var result = MackySoft.AgentSkills.Tiers.SkillTierLiteralParser.ParseSelectedTiers(UcliSkillTierLiterals.Defined, tiers);
        if (!result.IsSuccess)
        {
            errorResult = CommandResult.InvalidArgument(
                command,
                result.Failure!.Message);
            return null;
        }

        return result.Value!;
    }

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

        if (!string.IsNullOrWhiteSpace(repoRoot))
        {
            return NormalizeRequiredFullPath(command, "repoRoot", repoRoot, out errorResult);
        }

        return ResolveDefaultRepositoryRoot(command, out errorResult);
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

    private static string? ResolveDefaultRepositoryRoot (
        string command,
        out CommandResult? errorResult)
    {
        errorResult = null;
        try
        {
            var currentDirectoryPath = Path.GetFullPath(Environment.CurrentDirectory);
            return UcliStoragePathResolver.ResolveStorageRoot(currentDirectoryPath);
        }
        catch (Exception ex) when (PathFormatExceptionClassifier.IsPathFormatException(ex))
        {
            errorResult = CommandResult.InvalidArgument(
                command,
                $"Current working directory path is invalid: {Environment.CurrentDirectory}. {ex.Message}");
            return null;
        }
    }
}
