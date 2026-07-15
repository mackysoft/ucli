using MackySoft.Ucli.Contracts.Testing;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Testing.Run.Configuration;

/// <summary> Merges CLI values, profile values, and defaults into normalized test-run configuration. </summary>
internal static class TestRunConfigurationMerger
{
    /// <summary> Merges one command input and profile configuration into a normalized configuration. </summary>
    /// <param name="cli"> The interpreted CLI input values. </param>
    /// <param name="profile"> The optional loaded profile. </param>
    /// <param name="projectPath"> The resolved project path candidate selected before merge. </param>
    /// <returns> The merged normalized configuration. </returns>
    public static MergedTestRunConfiguration Merge (
        TestRunConfigurationRequest cli,
        TestRunProfile? profile,
        string projectPath)
    {
        ArgumentNullException.ThrowIfNull(cli);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);

        var mode = ResolveMode(cli);
        var (mergedRawTestPlatform, parsedTestPlatform) = ResolveTestPlatform(cli, profile);

        return new MergedTestRunConfiguration(
            ProjectPath: projectPath,
            Mode: mode,
            UnityVersion: StringValueNormalizer.TrimToNull(cli.UnityVersion ?? profile?.UnityVersion),
            UnityEditorPath: NormalizeOptionalValue(cli.UnityEditorPath ?? profile?.UnityEditorPath),
            TestPlatform: parsedTestPlatform,
            RawTestPlatform: mergedRawTestPlatform,
            TestFilter: StringValueNormalizer.TrimToNull(cli.TestFilter ?? profile?.TestFilter),
            TestCategories: NormalizeValues(cli.TestCategory, profile?.TestCategories),
            AssemblyNames: NormalizeValues(cli.AssemblyName, profile?.AssemblyNames),
            TimeoutMilliseconds: cli.TimeoutMilliseconds ?? profile?.Timeout);
    }

    /// <summary> Normalizes optional string values. </summary>
    /// <param name="value"> The optional string value. </param>
    /// <returns> The trimmed value when provided; otherwise <see langword="null" />. </returns>
    private static string? NormalizeOptionalValue (string? value)
    {
        if (!StringValueNormalizer.TryTrimToNonEmpty(value, out var normalizedValue))
        {
            return null;
        }

        return normalizedValue;
    }

    private static UnityExecutionMode ResolveMode (
        TestRunConfigurationRequest cli)
    {
        ArgumentNullException.ThrowIfNull(cli);

        if (cli.Mode.HasValue)
        {
            return cli.Mode!.Value;
        }

        return UnityExecutionMode.Auto;
    }

    private static (string RawValue, TestRunPlatform? ParsedValue) ResolveTestPlatform (
        TestRunConfigurationRequest cli,
        TestRunProfile? profile)
    {
        ArgumentNullException.ThrowIfNull(cli);

        if (cli.TestPlatform.HasValue)
        {
            var parsedValue = cli.TestPlatform.Value;
            return (TestRunPlatformCodec.ToValue(parsedValue), parsedValue);
        }

        var rawValue = profile?.TestPlatform ?? TestRunPlatformCodec.EditMode;
        var hasParsedValue = TestRunPlatformCodec.TryParse(rawValue, out var parsedValueFromProfile);
        return (rawValue, hasParsedValue ? parsedValueFromProfile : null);
    }

    /// <summary> Normalizes list values by trimming entries and removing duplicates. </summary>
    /// <param name="cliValues"> The optional CLI values. </param>
    /// <param name="profileValues"> The optional profile values. </param>
    /// <returns> The normalized distinct values. </returns>
    private static string[] NormalizeValues (
        string[]? cliValues,
        string[]? profileValues)
    {
        var source = cliValues ?? profileValues;
        if (source is null || source.Length == 0)
        {
            return Array.Empty<string>();
        }

        return source
            .Select(static value => value.Trim())
            .Where(static value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}
