using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Skills.Hosts.Registration;
using MackySoft.Ucli.Skills.Installation;

namespace MackySoft.Ucli.Hosting.Cli.Skills;

/// <summary> Normalizes common <c>ucli skills</c> command options. </summary>
internal static class SkillsCommandOptionNormalizer
{
    private const string ProjectScopeLiteral = "project";

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

    /// <summary> Normalizes a required project scope option. </summary>
    /// <param name="command"> The command name used for error results. </param>
    /// <param name="scope"> The raw scope option. </param>
    /// <param name="errorResult"> The emitted error result when normalization fails. </param>
    /// <returns> The normalized scope kind, or <see langword="null" /> when normalization fails. </returns>
    public static SkillScopeKind? NormalizeProjectScope (
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

        if (!string.Equals(scope, ProjectScopeLiteral, StringComparison.OrdinalIgnoreCase))
        {
            errorResult = CommandResult.InvalidArgument(command, $"Unsupported SKILL scope: {scope}. Supported scopes: {ProjectScopeLiteral}.");
            return null;
        }

        return SkillScopeKind.Project;
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
}
