using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.ReadIndex;

namespace MackySoft.Ucli.Configuration;

/// <summary> Provides filesystem-backed access to <c>.ucli/config.json</c>. </summary>
internal sealed class UcliConfigStore : IUcliConfigStore
{
    private const int SupportedSchemaVersion = 1;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    /// <summary> Resolves the absolute path to <c>.ucli/config.json</c> for a storage root. </summary>
    /// <param name="storageRoot">
    /// <para> The storage-root path used as the base directory. </para>
    /// <para> Must not be <see langword="null" />. </para>
    /// </param>
    /// <returns> The absolute config path. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="storageRoot" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="storageRoot" /> contains invalid path characters. </exception>
    /// <exception cref="NotSupportedException"> Thrown when <paramref name="storageRoot" /> uses an unsupported path format. </exception>
    /// <exception cref="PathTooLongException"> Thrown when <paramref name="storageRoot" /> exceeds platform path limits. </exception>
    public string GetConfigPath (string storageRoot)
    {
        return UcliStoragePathResolver.ResolveConfigPath(storageRoot);
    }

    /// <summary> Loads configuration values for a storage root. </summary>
    /// <param name="storageRoot">
    /// <para> The storage-root path from command context. </para>
    /// <para> <see langword="null" />, empty, and whitespace values return an invalid-argument result. </para>
    /// </param>
    /// <param name="cancellationToken"> A cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the config-load result. When <c>.ucli/config.json</c> does not exist, default config values are returned with <see cref="ConfigSource.Default" />. </returns>
    public async ValueTask<UcliConfigLoadResult> Load (
        string storageRoot,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(storageRoot))
        {
            return UcliConfigLoadResult.Failure(ExecutionError.InvalidArgument("Storage root path must not be empty."));
        }

        string configPath;
        try
        {
            configPath = GetConfigPath(storageRoot);
        }
        catch (Exception ex) when (IsPathFormatException(ex))
        {
            return UcliConfigLoadResult.Failure(ExecutionError.InvalidArgument(
                $"Storage root path is invalid: {storageRoot}"));
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
            return UcliConfigLoadResult.Failure(ExecutionError.InvalidArgument(
                $"Config path is invalid: {configPath}"));
        }
        catch (Exception ex) when (IsIoFailure(ex))
        {
            return UcliConfigLoadResult.Failure(ExecutionError.InternalError(
                $"Failed to read config file: {configPath}. {ex.Message}"));
        }

        UcliConfigJsonRawDocument document;
        try
        {
            using var jsonDocument = JsonDocument.Parse(json);
            if (!UcliConfigJsonContractReader.TryReadStrict(jsonDocument.RootElement, out document, out var readError))
            {
                return UcliConfigLoadResult.Failure(CreateConfigJsonReadError(readError, configPath));
            }
        }
        catch (JsonException ex)
        {
            return UcliConfigLoadResult.Failure(ExecutionError.InvalidArgument(
                $"Config JSON is invalid: {configPath}. {ex.Message}"));
        }

        var parseResult = TryConvertToConfig(document, configPath);
        if (!parseResult.IsSuccess)
        {
            return UcliConfigLoadResult.Failure(parseResult.Error!);
        }

        return UcliConfigLoadResult.Success(parseResult.Config!, ConfigSource.File);
    }

    /// <summary> Saves configuration values to <c>.ucli/config.json</c>. </summary>
    /// <param name="storageRoot">
    /// <para> The storage-root path from command context. </para>
    /// <para> <see langword="null" />, empty, and whitespace values return an invalid-argument result. </para>
    /// </param>
    /// <param name="config"> The config values to persist. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the config-save result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="config" /> is <see langword="null" />. </exception>
    public async ValueTask<UcliConfigSaveResult> Save (
        string storageRoot,
        UcliConfig config,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(storageRoot))
        {
            return UcliConfigSaveResult.Failure(ExecutionError.InvalidArgument("Storage root path must not be empty."));
        }

        ArgumentNullException.ThrowIfNull(config);

        string configPath;
        try
        {
            configPath = GetConfigPath(storageRoot);
        }
        catch (Exception ex) when (IsPathFormatException(ex))
        {
            return UcliConfigSaveResult.Failure(ExecutionError.InvalidArgument(
                $"Storage root path is invalid: {storageRoot}"));
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
            return UcliConfigSaveResult.Failure(ExecutionError.InvalidArgument(
                $"Config path is invalid: {configPath}. {ex.Message}"));
        }

        if (string.IsNullOrWhiteSpace(configDirectoryPath))
        {
            return UcliConfigSaveResult.Failure(ExecutionError.InternalError(
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
            return UcliConfigSaveResult.Failure(ExecutionError.InvalidArgument(
                $"Config path is invalid: {configPath}. {ex.Message}"));
        }
        catch (Exception ex) when (IsIoFailure(ex))
        {
            return UcliConfigSaveResult.Failure(ExecutionError.InternalError(
                $"Failed to write config file: {configPath}. {ex.Message}"));
        }
    }

    /// <summary> Converts raw config JSON values into a validated <see cref="UcliConfig" /> instance. </summary>
    /// <param name="document"> The raw config JSON contract document. </param>
    /// <param name="configPath"> The source config path. </param>
    /// <returns> The conversion result. </returns>
    private static ConfigParseResult TryConvertToConfig (
        UcliConfigJsonRawDocument document,
        string configPath)
    {
        if (document.SchemaVersion != SupportedSchemaVersion)
        {
            return ConfigParseResult.Failure(ExecutionError.InvalidArgument(
                $"Config schemaVersion must be {SupportedSchemaVersion}. Actual: {document.SchemaVersion}."));
        }

        var schemaVersion = document.SchemaVersion.GetValueOrDefault();

        if (!TryParseOperationPolicy(document.OperationPolicy, out var operationPolicy))
        {
            return ConfigParseResult.Failure(ExecutionError.InvalidArgument(
                $"Config operationPolicy is invalid: {document.OperationPolicy}."));
        }

        if (!PlanTokenModeCodec.TryParse(document.PlanTokenMode, out var planTokenMode))
        {
            return ConfigParseResult.Failure(ExecutionError.InvalidArgument(
                $"Config planTokenMode is invalid: {document.PlanTokenMode}."));
        }

        var readIndexDefaultModeValue = document.ReadIndexDefaultMode
            ?? UcliConfigValueConstants.ReadIndexModeRequireFresh;
        if (!TryParseReadIndexMode(readIndexDefaultModeValue, out var readIndexDefaultMode))
        {
            return ConfigParseResult.Failure(ExecutionError.InvalidArgument(
                $"Config readIndexDefaultMode is invalid: {readIndexDefaultModeValue}."));
        }

        var ipcDefaultTimeoutMillisecondsValue = document.IpcDefaultTimeoutMilliseconds
            ?? UcliConfig.DefaultIpcTimeoutMilliseconds;
        if (!IpcTimeoutConfigValidator.TryParseTimeoutMilliseconds(ipcDefaultTimeoutMillisecondsValue, out var ipcDefaultTimeoutMilliseconds))
        {
            return ConfigParseResult.Failure(ExecutionError.InvalidArgument(
                $"Config ipcDefaultTimeoutMilliseconds is invalid: {ipcDefaultTimeoutMillisecondsValue}."));
        }

        if (!IpcTimeoutConfigValidator.TryParseCommandTimeoutOverrides(
                document.IpcTimeoutMillisecondsByCommand,
                out var ipcTimeoutMillisecondsByCommand,
                out var ipcTimeoutByCommandError))
        {
            return ConfigParseResult.Failure(ipcTimeoutByCommandError!);
        }

        if (document.OperationAllowlist is null)
        {
            return ConfigParseResult.Failure(ExecutionError.InvalidArgument(
                $"Config operationAllowlist must be an array: {configPath}."));
        }

        var normalizedAllowlist = new List<string>(document.OperationAllowlist.Length);
        foreach (var pattern in document.OperationAllowlist)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                return ConfigParseResult.Failure(ExecutionError.InvalidArgument(
                    $"Config operationAllowlist contains an empty pattern: {configPath}."));
            }

            var normalizedPattern = pattern.Trim();
            if (!TryValidateRegexPattern(normalizedPattern, out var patternErrorMessage))
            {
                return ConfigParseResult.Failure(ExecutionError.InvalidArgument(
                    $"Config operationAllowlist contains an invalid regex pattern: {normalizedPattern}. {patternErrorMessage}"));
            }

            normalizedAllowlist.Add(normalizedPattern);
        }

        var config = new UcliConfig(
            SchemaVersion: schemaVersion,
            OperationPolicy: operationPolicy,
            PlanTokenMode: planTokenMode,
            ReadIndexDefaultMode: readIndexDefaultMode,
            OperationAllowlist: normalizedAllowlist)
        {
            IpcDefaultTimeoutMilliseconds = ipcDefaultTimeoutMilliseconds,
            IpcTimeoutMillisecondsByCommand = ipcTimeoutMillisecondsByCommand,
        };
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
            return ConfigValidationResult.Failure(ExecutionError.InvalidArgument(
                $"Config schemaVersion must be {SupportedSchemaVersion}. Actual: {config.SchemaVersion}."));
        }

        if (config.OperationAllowlist is null)
        {
            return ConfigValidationResult.Failure(ExecutionError.InvalidArgument(
                $"Config operationAllowlist must not be null: {configPath}."));
        }

        foreach (var pattern in config.OperationAllowlist)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                return ConfigValidationResult.Failure(ExecutionError.InvalidArgument(
                    $"Config operationAllowlist contains an empty pattern: {configPath}."));
            }

            if (!TryValidateRegexPattern(pattern, out var patternErrorMessage))
            {
                return ConfigValidationResult.Failure(ExecutionError.InvalidArgument(
                    $"Config operationAllowlist contains an invalid regex pattern: {pattern}. {patternErrorMessage}"));
            }
        }

        if (!IpcTimeoutConfigValidator.TryParseTimeoutMilliseconds(config.IpcDefaultTimeoutMilliseconds, out _))
        {
            return ConfigValidationResult.Failure(ExecutionError.InvalidArgument(
                $"Config ipcDefaultTimeoutMilliseconds must be a positive integer. Actual: {config.IpcDefaultTimeoutMilliseconds}."));
        }

        if (!IpcTimeoutConfigValidator.TryValidateCommandTimeoutOverrides(
                config.IpcTimeoutMillisecondsByCommand,
                out var ipcTimeoutByCommandError))
        {
            return ConfigValidationResult.Failure(ipcTimeoutByCommandError!);
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
        var ipcTimeoutMillisecondsByCommand = IpcTimeoutConfigValidator.CreateSerializableCommandTimeoutOverrides(
            config.IpcTimeoutMillisecondsByCommand);

        return new UcliConfigDocument(
            SchemaVersion: config.SchemaVersion,
            OperationPolicy: ToStringValue(config.OperationPolicy),
            PlanTokenMode: PlanTokenModeCodec.ToValue(config.PlanTokenMode),
            ReadIndexDefaultMode: ToStringValue(config.ReadIndexDefaultMode),
            OperationAllowlist: config.OperationAllowlist.ToArray(),
            IpcDefaultTimeoutMilliseconds: config.IpcDefaultTimeoutMilliseconds,
            IpcTimeoutMillisecondsByCommand: ipcTimeoutMillisecondsByCommand);
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

    /// <summary> Converts <see cref="ReadIndexMode" /> to the config string value. </summary>
    /// <param name="readIndexMode"> The read-index mode value. </param>
    /// <returns> The config string representation. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="readIndexMode" /> is outside supported values. </exception>
    private static string ToStringValue (ReadIndexMode readIndexMode)
    {
        return readIndexMode switch
        {
            ReadIndexMode.Disabled => UcliConfigValueConstants.ReadIndexModeDisabled,
            ReadIndexMode.AllowStale => UcliConfigValueConstants.ReadIndexModeAllowStale,
            ReadIndexMode.RequireFresh => UcliConfigValueConstants.ReadIndexModeRequireFresh,
            _ => throw new ArgumentOutOfRangeException(nameof(readIndexMode), readIndexMode, "Unsupported readIndexMode."),
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

    /// <summary> Parses read-index-mode config values. </summary>
    /// <param name="value"> The config string value. </param>
    /// <param name="readIndexMode"> The parsed enum value. </param>
    /// <returns> <see langword="true" /> when parse succeeds; otherwise <see langword="false" />. </returns>
    private static bool TryParseReadIndexMode (string? value, out ReadIndexMode readIndexMode)
    {
        if (string.Equals(value, UcliConfigValueConstants.ReadIndexModeDisabled, StringComparison.OrdinalIgnoreCase))
        {
            readIndexMode = ReadIndexMode.Disabled;
            return true;
        }

        if (string.Equals(value, UcliConfigValueConstants.ReadIndexModeAllowStale, StringComparison.OrdinalIgnoreCase))
        {
            readIndexMode = ReadIndexMode.AllowStale;
            return true;
        }

        if (string.Equals(value, UcliConfigValueConstants.ReadIndexModeRequireFresh, StringComparison.OrdinalIgnoreCase))
        {
            readIndexMode = ReadIndexMode.RequireFresh;
            return true;
        }

        readIndexMode = default;
        return false;
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

    /// <summary> Converts one machine-readable config JSON read error into execution error. </summary>
    /// <param name="readError"> The machine-readable config JSON read error. </param>
    /// <param name="configPath"> The source config path. </param>
    /// <returns> The mapped execution error. </returns>
    private static ExecutionError CreateConfigJsonReadError (
        UcliConfigJsonReadError readError,
        string configPath)
    {
        return readError.Kind switch
        {
            UcliConfigJsonReadErrorKind.RootTypeMismatch => ExecutionError.InvalidArgument(
                $"Config JSON root must be an object: {configPath}."),
            UcliConfigJsonReadErrorKind.MissingProperty => ExecutionError.InvalidArgument(
                $"Config JSON is missing required property: {readError.PropertyName}. {configPath}"),
            UcliConfigJsonReadErrorKind.PropertyTypeMismatch => ExecutionError.InvalidArgument(
                $"Config JSON property type is invalid: {readError.PropertyName}. {configPath}"),
            UcliConfigJsonReadErrorKind.ArrayElementTypeMismatch => ExecutionError.InvalidArgument(
                $"Config JSON array element type is invalid: {readError.PropertyName}. {configPath}"),
            UcliConfigJsonReadErrorKind.ObjectPropertyTypeMismatch => ExecutionError.InvalidArgument(
                $"Config JSON object property type is invalid: {readError.PropertyName}. {configPath}"),
            UcliConfigJsonReadErrorKind.UnknownProperty => ExecutionError.InvalidArgument(
                $"Config contains unknown properties: {readError.PropertyName}."),
            _ => ExecutionError.InvalidArgument(
                $"Config JSON is invalid: {configPath}."),
        };
    }

    /// <summary> Serializable JSON DTO for config values. </summary>
    /// <param name="SchemaVersion"> The config schema version. </param>
    /// <param name="OperationPolicy"> The operation-policy value. </param>
    /// <param name="PlanTokenMode"> The plan-token-mode value. </param>
    /// <param name="ReadIndexDefaultMode"> The read-index default mode value. </param>
    /// <param name="OperationAllowlist"> The operation-name allowlist. </param>
    /// <param name="IpcDefaultTimeoutMilliseconds"> The IPC default timeout value in milliseconds. </param>
    /// <param name="IpcTimeoutMillisecondsByCommand">
    /// <para> The per-command IPC timeout overrides in milliseconds. </para>
    /// <para> <see langword="null" /> means that default command entries are generated during parse. </para>
    /// </param>
    private sealed record UcliConfigDocument (
        int SchemaVersion,
        string OperationPolicy,
        string PlanTokenMode,
        string? ReadIndexDefaultMode,
        string[] OperationAllowlist,
        int? IpcDefaultTimeoutMilliseconds,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        Dictionary<string, int?>? IpcTimeoutMillisecondsByCommand);

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