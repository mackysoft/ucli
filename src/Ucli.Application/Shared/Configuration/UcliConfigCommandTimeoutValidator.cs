using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Application.Shared.Configuration;

/// <summary> Validates and normalizes per-command IPC timeout overrides from config values. </summary>
internal static class UcliConfigCommandTimeoutValidator
{
    /// <summary> Builds normalized command timeout overrides while adding diagnostics for invalid entries. </summary>
    public static Dictionary<string, int?> BuildNormalizedOverrides (
        IReadOnlyDictionary<string, int?> source,
        string sourcePath,
        string unsupportedCommandCode,
        string invalidTimeoutCode,
        List<UcliConfigDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(source);
        ValidateArguments(sourcePath, unsupportedCommandCode, invalidTimeoutCode, diagnostics);

        var timeoutsByCommand = new Dictionary<string, int?>(StringComparer.Ordinal);
        foreach (var entry in source)
        {
            var commandKey = UcliConfigDiagnostic.FormatFragment(entry.Key);
            var propertyPath = $"{UcliConfigJsonPropertyNames.IpcTimeoutMillisecondsByCommand}.{commandKey}";
            if (!IpcTimeoutConfigValidator.TryNormalizeSupportedCommandName(entry.Key, out var commandName))
            {
                if (!AddUnsupportedCommandDiagnostic(
                    diagnostics,
                    unsupportedCommandCode,
                    propertyPath,
                    commandKey,
                    sourcePath))
                {
                    break;
                }

                continue;
            }

            if (!TryValidateTimeoutValue(entry.Value, out var invalidTimeoutValue))
            {
                if (!AddDiagnostic(diagnostics, CreateLoadInvalidTimeoutDiagnostic(
                    invalidTimeoutCode,
                    propertyPath,
                    invalidTimeoutValue,
                    sourcePath)))
                {
                    break;
                }

                continue;
            }

            timeoutsByCommand[commandName] = entry.Value;
        }

        return timeoutsByCommand;
    }

    /// <summary> Adds save-time diagnostics for command timeout overrides. </summary>
    public static void AddSaveDiagnostics (
        IReadOnlyDictionary<string, int?> source,
        string sourcePath,
        string unsupportedCommandCode,
        string invalidTimeoutCode,
        List<UcliConfigDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(source);
        ValidateArguments(sourcePath, unsupportedCommandCode, invalidTimeoutCode, diagnostics);

        foreach (var entry in source)
        {
            var commandKey = UcliConfigDiagnostic.FormatFragment(entry.Key);
            var propertyPath = $"{UcliConfigJsonPropertyNames.IpcTimeoutMillisecondsByCommand}.{commandKey}";
            if (!IpcTimeoutConfigValidator.TryNormalizeSupportedCommandName(entry.Key, out _))
            {
                if (!AddUnsupportedCommandDiagnostic(
                    diagnostics,
                    unsupportedCommandCode,
                    propertyPath,
                    commandKey,
                    sourcePath))
                {
                    break;
                }

                continue;
            }

            if (!TryValidateTimeoutValue(entry.Value, out var invalidTimeoutValue))
            {
                if (!AddDiagnostic(diagnostics, CreateSaveInvalidTimeoutDiagnostic(
                    invalidTimeoutCode,
                    propertyPath,
                    commandKey,
                    invalidTimeoutValue,
                    sourcePath)))
                {
                    break;
                }

                continue;
            }
        }
    }

    private static bool AddUnsupportedCommandDiagnostic (
        List<UcliConfigDiagnostic> diagnostics,
        string unsupportedCommandCode,
        string propertyPath,
        string commandKey,
        string sourcePath)
    {
        var supportedCommands = IpcTimeoutConfigValidator.GetSupportedCommandNamesDescription();
        return AddDiagnostic(diagnostics, CreateDiagnostic(
            unsupportedCommandCode,
            propertyPath,
            sourcePath,
            $"Config ipcTimeoutMillisecondsByCommand contains unsupported command key: {commandKey}. Supported: {supportedCommands}."));
    }

    private static bool TryValidateTimeoutValue (
        int? timeoutMilliseconds,
        out int invalidTimeoutMilliseconds)
    {
        if (timeoutMilliseconds.HasValue
            && !IpcTimeoutConfigValidator.TryParseTimeoutMilliseconds(timeoutMilliseconds.Value, out _))
        {
            invalidTimeoutMilliseconds = timeoutMilliseconds.Value;
            return false;
        }

        invalidTimeoutMilliseconds = default;
        return true;
    }

    private static UcliConfigDiagnostic CreateLoadInvalidTimeoutDiagnostic (
        string code,
        string propertyPath,
        int actualValue,
        string sourcePath)
    {
        return CreateDiagnostic(
            code,
            propertyPath,
            sourcePath,
            $"Config {propertyPath} is invalid: {actualValue}.");
    }

    private static UcliConfigDiagnostic CreateSaveInvalidTimeoutDiagnostic (
        string code,
        string propertyPath,
        string commandKey,
        int actualValue,
        string sourcePath)
    {
        return CreateDiagnostic(
            code,
            propertyPath,
            sourcePath,
            $"Config ipcTimeoutMillisecondsByCommand[{commandKey}] must be a positive integer or null. Actual: {actualValue}.");
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

    private static void ValidateArguments (
        string sourcePath,
        string unsupportedCommandCode,
        string invalidTimeoutCode,
        List<UcliConfigDiagnostic> diagnostics)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(unsupportedCommandCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(invalidTimeoutCode);
        ArgumentNullException.ThrowIfNull(diagnostics);
    }
}
