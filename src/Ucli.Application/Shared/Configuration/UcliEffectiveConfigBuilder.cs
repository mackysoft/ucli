using MackySoft.Ucli.Contracts.Configuration;

using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Shared.Configuration;

/// <summary> Builds effective config values from raw config JSON values. </summary>
internal sealed class UcliEffectiveConfigBuilder
{
    private const string UnsupportedSchemaVersionCode = "config.semantic.unsupportedSchemaVersion";
    private const string UnsupportedLiteralCode = "config.semantic.unsupportedLiteral";
    private const string EmptyAllowlistPatternCode = "config.semantic.emptyAllowlistPattern";
    private const string InvalidRegexPatternCode = "config.semantic.invalidRegexPattern";
    private const string InvalidTimeoutCode = "config.semantic.invalidTimeout";
    private const string UnsupportedTimeoutCommandCode = "config.semantic.unsupportedTimeoutCommand";

    /// <summary> Builds effective config values from raw config JSON values. </summary>
    /// <param name="document"> The raw config JSON values. </param>
    /// <param name="sourcePath"> The source config path used in diagnostics. </param>
    /// <returns> The build result. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="sourcePath" /> is empty. </exception>
    public UcliConfigBuildResult Build (
        UcliConfigJsonRawDocument document,
        string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        var diagnostics = new List<UcliConfigDiagnostic>();

        if (document.SchemaVersion != UcliConfig.CurrentSchemaVersion)
        {
            AddDiagnostic(diagnostics, CreateDiagnostic(
                UnsupportedSchemaVersionCode,
                UcliConfigJsonPropertyNames.SchemaVersion,
                sourcePath,
                $"Config schemaVersion must be {UcliConfig.CurrentSchemaVersion}. Actual: {FormatValue(document.SchemaVersion)}."));
        }

        if (!VocabularyInputParser.TryParseIgnoreCase<OperationPolicy>(document.OperationPolicy, out var operationPolicy))
        {
            AddDiagnostic(diagnostics, CreateUnsupportedLiteralDiagnostic(
                UcliConfigJsonPropertyNames.OperationPolicy,
                document.OperationPolicy,
                sourcePath));
        }

        if (!VocabularyInputParser.TryParseIgnoreCase<PlanTokenMode>(document.PlanTokenMode, out var planTokenMode))
        {
            AddDiagnostic(diagnostics, CreateUnsupportedLiteralDiagnostic(
                UcliConfigJsonPropertyNames.PlanTokenMode,
                document.PlanTokenMode,
                sourcePath));
        }

        var readIndexDefaultModeValue = document.ReadIndexDefaultMode
            ?? TextVocabulary.GetText(ReadIndexMode.RequireFresh);
        if (!VocabularyInputParser.TryParseIgnoreCase<ReadIndexMode>(readIndexDefaultModeValue, out var readIndexDefaultMode))
        {
            AddDiagnostic(diagnostics, CreateUnsupportedLiteralDiagnostic(
                UcliConfigJsonPropertyNames.ReadIndexDefaultMode,
                readIndexDefaultModeValue,
                sourcePath));
        }

        var ipcDefaultTimeoutMillisecondsValue = document.IpcDefaultTimeoutMilliseconds
            ?? IpcTimeoutDefaults.GlobalTimeoutMilliseconds;
        if (!IpcTimeoutConfigValidator.TryParseTimeoutMilliseconds(ipcDefaultTimeoutMillisecondsValue, out var ipcDefaultTimeoutMilliseconds))
        {
            AddDiagnostic(diagnostics, CreateInvalidTimeoutDiagnostic(
                UcliConfigJsonPropertyNames.IpcDefaultTimeoutMilliseconds,
                ipcDefaultTimeoutMillisecondsValue,
                sourcePath));
        }

        var ipcTimeoutMillisecondsByCommand = BuildCommandTimeoutOverrides(
            document.IpcTimeoutMillisecondsByCommand,
            sourcePath,
            diagnostics);
        var operationAllowlist = BuildOperationAllowlist(
            document.OperationAllowlist,
            sourcePath,
            diagnostics);

        if (diagnostics.Count > 0)
        {
            return UcliConfigBuildResult.Failure(diagnostics);
        }

        var config = new UcliConfig(
            SchemaVersion: document.SchemaVersion,
            OperationPolicy: operationPolicy,
            PlanTokenMode: planTokenMode,
            ReadIndexDefaultMode: readIndexDefaultMode,
            OperationAllowlist: operationAllowlist)
        {
            IpcDefaultTimeoutMilliseconds = ipcDefaultTimeoutMilliseconds,
            IpcTimeoutMillisecondsByCommand = ipcTimeoutMillisecondsByCommand,
        };
        return UcliConfigBuildResult.Success(config);
    }

    private static Dictionary<string, int?> BuildCommandTimeoutOverrides (
        IReadOnlyDictionary<string, int?>? source,
        string sourcePath,
        List<UcliConfigDiagnostic> diagnostics)
    {
        if (source is null)
        {
            return IpcTimeoutDefaults.CreateDefaultTimeoutOverrides();
        }

        return UcliConfigCommandTimeoutValidator.BuildNormalizedOverrides(
            source,
            sourcePath,
            UnsupportedTimeoutCommandCode,
            InvalidTimeoutCode,
            diagnostics);
    }

    private static List<string> BuildOperationAllowlist (
        IReadOnlyList<string> source,
        string sourcePath,
        List<UcliConfigDiagnostic> diagnostics)
    {
        return UcliConfigOperationAllowlistValidator.BuildNormalizedPatterns(
            source,
            sourcePath,
            EmptyAllowlistPatternCode,
            InvalidRegexPatternCode,
            diagnostics);
    }

    private static UcliConfigDiagnostic CreateUnsupportedLiteralDiagnostic (
        string propertyPath,
        string? actualValue,
        string sourcePath)
    {
        return CreateDiagnostic(
            UnsupportedLiteralCode,
            propertyPath,
            sourcePath,
            $"Config {propertyPath} is invalid: {FormatValue(actualValue)}.");
    }

    private static UcliConfigDiagnostic CreateInvalidTimeoutDiagnostic (
        string propertyPath,
        int actualValue,
        string sourcePath)
    {
        return CreateDiagnostic(
            InvalidTimeoutCode,
            propertyPath,
            sourcePath,
            $"Config {propertyPath} is invalid: {actualValue}.");
    }

    private static UcliConfigDiagnostic CreateDiagnostic (
        string code,
        string propertyPath,
        string sourcePath,
        string message)
    {
        return UcliConfigDiagnostic.Create(code, propertyPath, sourcePath, message);
    }

    private static bool AddDiagnostic (
        List<UcliConfigDiagnostic> diagnostics,
        UcliConfigDiagnostic diagnostic)
    {
        return UcliConfigDiagnosticList.Add(diagnostics, diagnostic);
    }

    private static string FormatValue<T> (T? value)
    {
        return UcliConfigDiagnostic.FormatFragment(value?.ToString());
    }
}
