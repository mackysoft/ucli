using System.Text.Json;
using System.Text.RegularExpressions;
using MackySoft.Ucli.Foundation;

namespace MackySoft.Ucli.Configuration;

/// <summary> Provides filesystem-backed access to <c>.ucli/config.json</c>. </summary>
internal sealed class UcliConfigStore : IUcliConfigStore
{
    private const string UcliDirectoryName = ".ucli";
    private const string ConfigFileName = "config.json";
    private const int SupportedSchemaVersion = 1;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    /// <summary> Resolves the absolute path to <c>.ucli/config.json</c> for a UnityProject root. </summary>
    /// <param name="unityProjectRoot"> The UnityProject root path used as the base directory. Must not be <see langword="null" />. </param>
    /// <returns> The absolute config path. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProjectRoot" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="unityProjectRoot" /> contains invalid path characters. </exception>
    /// <exception cref="NotSupportedException"> Thrown when <paramref name="unityProjectRoot" /> uses an unsupported path format. </exception>
    /// <exception cref="PathTooLongException"> Thrown when <paramref name="unityProjectRoot" /> exceeds platform path limits. </exception>
    public string GetConfigPath (string unityProjectRoot)
    {
        var fullPath = Path.GetFullPath(unityProjectRoot);
        return Path.Combine(fullPath, UcliDirectoryName, ConfigFileName);
    }

    /// <summary> Loads configuration values for a UnityProject root. </summary>
    /// <param name="unityProjectRoot"> The UnityProject root path from command context. <see langword="null" />, empty, and whitespace values return an invalid-argument result. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the config-load result. When <c>.ucli/config.json</c> does not exist, default config values are returned with <see cref="ConfigSource.Default" />. </returns>
    public async ValueTask<UcliConfigLoadResult> Load (
        string unityProjectRoot,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(unityProjectRoot))
        {
            return UcliConfigLoadResult.Failure(CreateInvalidArgument("UnityProject root path must not be empty."));
        }

        string configPath;
        try
        {
            configPath = GetConfigPath(unityProjectRoot);
        }
        catch (Exception ex) when (IsPathFormatException(ex))
        {
            return UcliConfigLoadResult.Failure(CreateInvalidArgument(
                $"UnityProject root path is invalid: {unityProjectRoot}"));
        }

        if (!File.Exists(configPath))
        {
            return UcliConfigLoadResult.Success(UcliConfig.CreateDefault(), ConfigSource.Default);
        }

        string json;
        try
        {
            json = await File.ReadAllTextAsync(configPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsPathFormatException(ex))
        {
            return UcliConfigLoadResult.Failure(CreateInvalidArgument(
                $"Config path is invalid: {configPath}"));
        }
        catch (Exception ex) when (IsIoFailure(ex))
        {
            return UcliConfigLoadResult.Failure(CreateInternalError(
                $"Failed to read config file: {configPath}. {ex.Message}"));
        }

        UcliConfigDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<UcliConfigDocument>(json, SerializerOptions);
        }
        catch (JsonException ex)
        {
            return UcliConfigLoadResult.Failure(CreateInvalidArgument(
                $"Config JSON is invalid: {configPath}. {ex.Message}"));
        }

        if (document is null)
        {
            return UcliConfigLoadResult.Failure(CreateInvalidArgument(
                $"Config JSON is invalid: {configPath}."));
        }

        var parseResult = TryConvertToConfig(document, configPath);
        if (!parseResult.IsSuccess)
        {
            return UcliConfigLoadResult.Failure(parseResult.Error!);
        }

        return UcliConfigLoadResult.Success(parseResult.Config!, ConfigSource.File);
    }

    /// <summary> Saves configuration values to <c>.ucli/config.json</c>. </summary>
    /// <param name="unityProjectRoot"> The UnityProject root path from command context. <see langword="null" />, empty, and whitespace values return an invalid-argument result. </param>
    /// <param name="config"> The config values to persist. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the config-save result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="config" /> is <see langword="null" />. </exception>
    public async ValueTask<UcliConfigSaveResult> Save (
        string unityProjectRoot,
        UcliConfig config,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(unityProjectRoot))
        {
            return UcliConfigSaveResult.Failure(CreateInvalidArgument("UnityProject root path must not be empty."));
        }

        ArgumentNullException.ThrowIfNull(config);

        string configPath;
        try
        {
            configPath = GetConfigPath(unityProjectRoot);
        }
        catch (Exception ex) when (IsPathFormatException(ex))
        {
            return UcliConfigSaveResult.Failure(CreateInvalidArgument(
                $"UnityProject root path is invalid: {unityProjectRoot}"));
        }

        var configValidationResult = ValidateConfig(config, configPath);
        if (!configValidationResult.IsSuccess)
        {
            return UcliConfigSaveResult.Failure(configValidationResult.Error!);
        }

        var json = JsonSerializer.Serialize(ToDocument(config), SerializerOptions);
        string? configDirectoryPath = null;
        try
        {
            configDirectoryPath = Path.GetDirectoryName(configPath);
        }
        catch (Exception ex) when (IsPathFormatException(ex))
        {
            return UcliConfigSaveResult.Failure(CreateInvalidArgument(
                $"Config path is invalid: {configPath}. {ex.Message}"));
        }

        if (string.IsNullOrWhiteSpace(configDirectoryPath))
        {
            return UcliConfigSaveResult.Failure(CreateInternalError(
                $"Config directory path could not be determined: {configPath}"));
        }

        try
        {
            Directory.CreateDirectory(configDirectoryPath);
            await File.WriteAllTextAsync(configPath, json + Environment.NewLine, cancellationToken).ConfigureAwait(false);
            return UcliConfigSaveResult.Success();
        }
        catch (Exception ex) when (IsPathFormatException(ex))
        {
            return UcliConfigSaveResult.Failure(CreateInvalidArgument(
                $"Config path is invalid: {configPath}. {ex.Message}"));
        }
        catch (Exception ex) when (IsIoFailure(ex))
        {
            return UcliConfigSaveResult.Failure(CreateInternalError(
                $"Failed to write config file: {configPath}. {ex.Message}"));
        }
    }

    /// <summary> Converts deserialized config JSON into a validated <see cref="UcliConfig" /> instance. </summary>
    /// <param name="document"> The deserialized config document. </param>
    /// <param name="configPath"> The source config path. </param>
    /// <returns> The conversion result. </returns>
    private static ConfigParseResult TryConvertToConfig (
        UcliConfigDocument document,
        string configPath)
    {
        if (document.SchemaVersion != SupportedSchemaVersion)
        {
            return ConfigParseResult.Failure(CreateInvalidArgument(
                $"Config schemaVersion must be {SupportedSchemaVersion}. Actual: {document.SchemaVersion}."));
        }

        if (!TryParseOperationPolicy(document.OperationPolicy, out var operationPolicy))
        {
            return ConfigParseResult.Failure(CreateInvalidArgument(
                $"Config operationPolicy is invalid: {document.OperationPolicy}."));
        }

        if (!TryParsePlanTokenMode(document.PlanTokenMode, out var planTokenMode))
        {
            return ConfigParseResult.Failure(CreateInvalidArgument(
                $"Config planTokenMode is invalid: {document.PlanTokenMode}."));
        }

        if (document.OperationAllowlist is null)
        {
            return ConfigParseResult.Failure(CreateInvalidArgument(
                $"Config operationAllowlist must be an array: {configPath}."));
        }

        var normalizedAllowlist = new List<string>(document.OperationAllowlist.Length);
        foreach (var pattern in document.OperationAllowlist)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                return ConfigParseResult.Failure(CreateInvalidArgument(
                    $"Config operationAllowlist contains an empty pattern: {configPath}."));
            }

            var normalizedPattern = pattern.Trim();
            if (!TryValidateRegexPattern(normalizedPattern, out var patternErrorMessage))
            {
                return ConfigParseResult.Failure(CreateInvalidArgument(
                    $"Config operationAllowlist contains an invalid regex pattern: {normalizedPattern}. {patternErrorMessage}"));
            }

            normalizedAllowlist.Add(normalizedPattern);
        }

        var config = new UcliConfig(
            SchemaVersion: document.SchemaVersion,
            OperationPolicy: operationPolicy,
            PlanTokenMode: planTokenMode,
            OperationAllowlist: normalizedAllowlist);
        return ConfigParseResult.Success(config);
    }

    /// <summary> Validates configuration values before writing them to disk. </summary>
    /// <param name="config"> The config values to validate. </param>
    /// <param name="configPath"> The destination config path. </param>
    /// <returns> The validation result. </returns>
    private static ConfigValidationResult ValidateConfig (
        UcliConfig config,
        string configPath)
    {
        if (config.SchemaVersion != SupportedSchemaVersion)
        {
            return ConfigValidationResult.Failure(CreateInvalidArgument(
                $"Config schemaVersion must be {SupportedSchemaVersion}. Actual: {config.SchemaVersion}."));
        }

        if (config.OperationAllowlist is null)
        {
            return ConfigValidationResult.Failure(CreateInvalidArgument(
                $"Config operationAllowlist must not be null: {configPath}."));
        }

        foreach (var pattern in config.OperationAllowlist)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                return ConfigValidationResult.Failure(CreateInvalidArgument(
                    $"Config operationAllowlist contains an empty pattern: {configPath}."));
            }

            if (!TryValidateRegexPattern(pattern, out var patternErrorMessage))
            {
                return ConfigValidationResult.Failure(CreateInvalidArgument(
                    $"Config operationAllowlist contains an invalid regex pattern: {pattern}. {patternErrorMessage}"));
            }
        }

        return ConfigValidationResult.Success();
    }

    /// <summary> Validates whether a value can be compiled as a regular expression pattern. </summary>
    /// <param name="pattern"> The regex pattern string to validate. </param>
    /// <param name="errorMessage"> The parser error message when the pattern is invalid. </param>
    /// <returns> <see langword="true" /> when the pattern is valid; otherwise <see langword="false" />. </returns>
    private static bool TryValidateRegexPattern (
        string pattern,
        out string? errorMessage)
    {
        try
        {
            new Regex(pattern, RegexOptions.CultureInvariant);
            errorMessage = null;
            return true;
        }
        catch (ArgumentException exception)
        {
            errorMessage = exception.Message;
            return false;
        }
    }

    /// <summary> Converts typed config values into serializable JSON DTO values. </summary>
    /// <param name="config"> The typed config values. </param>
    /// <returns> The serializable DTO. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="config" /> is <see langword="null" />. </exception>
    private static UcliConfigDocument ToDocument (UcliConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return new UcliConfigDocument(
            SchemaVersion: config.SchemaVersion,
            OperationPolicy: ToStringValue(config.OperationPolicy),
            PlanTokenMode: ToStringValue(config.PlanTokenMode),
            OperationAllowlist: config.OperationAllowlist.ToArray());
    }

    /// <summary> Converts <see cref="OperationPolicy" /> to the config string value. </summary>
    /// <param name="operationPolicy"> The operation policy value. </param>
    /// <returns> The config string representation. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="operationPolicy" /> is outside supported values. </exception>
    private static string ToStringValue (OperationPolicy operationPolicy)
    {
        return operationPolicy switch
        {
            OperationPolicy.Safe => UcliConfigValueConstants.OperationPolicySafe,
            OperationPolicy.Advanced => UcliConfigValueConstants.OperationPolicyAdvanced,
            OperationPolicy.Dangerous => UcliConfigValueConstants.OperationPolicyDangerous,
            _ => throw new ArgumentOutOfRangeException(nameof(operationPolicy), operationPolicy, "Unsupported operationPolicy."),
        };
    }

    /// <summary> Converts <see cref="PlanTokenMode" /> to the config string value. </summary>
    /// <param name="planTokenMode"> The plan token mode value. </param>
    /// <returns> The config string representation. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="planTokenMode" /> is outside supported values. </exception>
    private static string ToStringValue (PlanTokenMode planTokenMode)
    {
        return planTokenMode switch
        {
            PlanTokenMode.Optional => UcliConfigValueConstants.PlanTokenModeOptional,
            PlanTokenMode.Required => UcliConfigValueConstants.PlanTokenModeRequired,
            _ => throw new ArgumentOutOfRangeException(nameof(planTokenMode), planTokenMode, "Unsupported planTokenMode."),
        };
    }

    /// <summary> Parses operation-policy config values. </summary>
    /// <param name="value"> The config string value. </param>
    /// <param name="operationPolicy"> The parsed enum value. </param>
    /// <returns> <see langword="true" /> when parse succeeds; otherwise <see langword="false" />. </returns>
    private static bool TryParseOperationPolicy (string? value, out OperationPolicy operationPolicy)
    {
        if (string.Equals(value, UcliConfigValueConstants.OperationPolicySafe, StringComparison.OrdinalIgnoreCase))
        {
            operationPolicy = OperationPolicy.Safe;
            return true;
        }

        if (string.Equals(value, UcliConfigValueConstants.OperationPolicyAdvanced, StringComparison.OrdinalIgnoreCase))
        {
            operationPolicy = OperationPolicy.Advanced;
            return true;
        }

        if (string.Equals(value, UcliConfigValueConstants.OperationPolicyDangerous, StringComparison.OrdinalIgnoreCase))
        {
            operationPolicy = OperationPolicy.Dangerous;
            return true;
        }

        operationPolicy = default;
        return false;
    }

    /// <summary> Parses plan-token-mode config values. </summary>
    /// <param name="value"> The config string value. </param>
    /// <param name="planTokenMode"> The parsed enum value. </param>
    /// <returns> <see langword="true" /> when parse succeeds; otherwise <see langword="false" />. </returns>
    private static bool TryParsePlanTokenMode (string? value, out PlanTokenMode planTokenMode)
    {
        if (string.Equals(value, UcliConfigValueConstants.PlanTokenModeOptional, StringComparison.OrdinalIgnoreCase))
        {
            planTokenMode = PlanTokenMode.Optional;
            return true;
        }

        if (string.Equals(value, UcliConfigValueConstants.PlanTokenModeRequired, StringComparison.OrdinalIgnoreCase))
        {
            planTokenMode = PlanTokenMode.Required;
            return true;
        }

        planTokenMode = default;
        return false;
    }

    /// <summary> Creates an invalid-argument error. </summary>
    /// <param name="message"> The error message. </param>
    /// <returns> The structured invalid-argument error. </returns>
    private static ExecutionError CreateInvalidArgument (string message)
    {
        return new ExecutionError(ExecutionErrorKind.InvalidArgument, message);
    }

    /// <summary> Creates an internal-error value. </summary>
    /// <param name="message"> The error message. </param>
    /// <returns> The structured internal-error value. </returns>
    private static ExecutionError CreateInternalError (string message)
    {
        return new ExecutionError(ExecutionErrorKind.InternalError, message);
    }

    /// <summary> Determines whether an exception should be treated as invalid path formatting. </summary>
    /// <param name="exception"> The exception to classify. </param>
    /// <returns> <see langword="true" /> when invalid path formatting is detected; otherwise <see langword="false" />. </returns>
    private static bool IsPathFormatException (Exception exception)
    {
        return exception is ArgumentException
            or NotSupportedException
            or PathTooLongException;
    }

    /// <summary> Determines whether an exception should be treated as an internal I/O failure. </summary>
    /// <param name="exception"> The exception to classify. </param>
    /// <returns> <see langword="true" /> when it is an I/O failure; otherwise <see langword="false" />. </returns>
    private static bool IsIoFailure (Exception exception)
    {
        return exception is IOException
            or UnauthorizedAccessException;
    }

    /// <summary> Serializable JSON DTO for config values. </summary>
    /// <param name="SchemaVersion"> The config schema version. </param>
    /// <param name="OperationPolicy"> The operation-policy value. </param>
    /// <param name="PlanTokenMode"> The plan-token-mode value. </param>
    /// <param name="OperationAllowlist"> The operation-name allowlist. </param>
    private sealed record UcliConfigDocument (
        int SchemaVersion,
        string OperationPolicy,
        string PlanTokenMode,
        string[] OperationAllowlist);

    /// <summary> Represents result values from config parse operations. </summary>
    /// <param name="Config"> The parsed config instance. </param>
    /// <param name="Error"> The parse error, when parse fails. </param>
    private readonly record struct ConfigParseResult (
        UcliConfig? Config,
        ExecutionError? Error)
    {
        /// <summary> Gets a value indicating whether parse succeeded. </summary>
        public bool IsSuccess => Config is not null && Error is null;

        /// <summary> Creates a successful parse result. </summary>
        /// <param name="config"> The parsed config instance. </param>
        /// <returns> The successful parse result. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="config" /> is <see langword="null" />. </exception>
        public static ConfigParseResult Success (UcliConfig config)
        {
            ArgumentNullException.ThrowIfNull(config);
            return new ConfigParseResult(config, null);
        }

        /// <summary> Creates a failed parse result. </summary>
        /// <param name="error"> The parse error. </param>
        /// <returns> The failed parse result. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
        public static ConfigParseResult Failure (ExecutionError error)
        {
            ArgumentNullException.ThrowIfNull(error);
            return new ConfigParseResult(null, error);
        }
    }

    /// <summary> Represents result values from config validation before save. </summary>
    /// <param name="Error"> The validation error, when validation fails. </param>
    private readonly record struct ConfigValidationResult (
        ExecutionError? Error)
    {
        /// <summary> Gets a value indicating whether validation succeeded. </summary>
        public bool IsSuccess => Error is null;

        /// <summary> Creates a successful validation result. </summary>
        /// <returns> The successful validation result. </returns>
        public static ConfigValidationResult Success ()
        {
            return new ConfigValidationResult(Error: null);
        }

        /// <summary> Creates a failed validation result. </summary>
        /// <param name="error"> The validation error. </param>
        /// <returns> The failed validation result. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
        public static ConfigValidationResult Failure (ExecutionError error)
        {
            ArgumentNullException.ThrowIfNull(error);
            return new ConfigValidationResult(Error: error);
        }
    }
}