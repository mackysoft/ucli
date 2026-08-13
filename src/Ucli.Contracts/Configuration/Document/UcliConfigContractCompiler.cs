using System.Text.Json;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Configuration;

/// <summary> Compiles the closed project configuration contract shared by the CLI and Unity. </summary>
internal sealed class UcliConfigContractCompiler
{
    internal const int CurrentSchemaVersion = 1;
    internal const int DefaultIpcTimeoutMilliseconds = 3000;
    private const int MaxDiagnostics = 50;
    private const string OmittedDiagnosticsCode = "config.diagnostics.omitted";
    private const string OmittedDiagnosticsMessage = "Additional config diagnostics were omitted.";

    private static readonly HashSet<string> AllowedProperties = new(StringComparer.Ordinal)
    {
        UcliConfigJsonPropertyNames.SchemaVersion,
        UcliConfigJsonPropertyNames.OperationPolicy,
        UcliConfigJsonPropertyNames.PlanTokenMode,
        UcliConfigJsonPropertyNames.ReadIndexDefaultMode,
        UcliConfigJsonPropertyNames.OperationAllowlist,
        UcliConfigJsonPropertyNames.EvalEnabled,
        UcliConfigJsonPropertyNames.IpcDefaultTimeoutMilliseconds,
        UcliConfigJsonPropertyNames.IpcTimeoutMillisecondsByCommand,
        UcliConfigJsonPropertyNames.ProgramPresets,
        UcliConfigJsonPropertyNames.WorkCompletion,
    };

    private static readonly HashSet<string> SupportedTimeoutCommands = new(StringComparer.Ordinal)
    {
        UcliCommandIds.Test.Name,
        UcliCommandIds.Ready.Name,
        UcliCommandIds.Compile.Name,
        UcliCommandIds.BuildRun.Name,
        UcliCommandIds.Verify.Name,
        UcliCommandIds.Status.Name,
        UcliCommandIds.Validate.Name,
        UcliCommandIds.Plan.Name,
        UcliCommandIds.Call.Name,
        UcliCommandIds.Eval.Name,
        UcliCommandIds.Resolve.Name,
        UcliCommandIds.Query.Name,
        UcliCommandIds.Refresh.Name,
        UcliCommandIds.Ops.Name,
        UcliCommandIds.DaemonStart.Name,
        UcliCommandIds.DaemonStop.Name,
        UcliCommandIds.DaemonCleanup.Name,
        UcliCommandIds.DaemonStatus.Name,
        UcliCommandIds.DaemonList.Name,
        UcliCommandIds.LogsDaemonRead.Name,
        UcliCommandIds.LogsUnityRead.Name,
        UcliCommandIds.LogsUnityClear.Name,
        UcliCommandIds.Screenshot.Name,
        UcliCommandIds.RecordingStart.Name,
        UcliCommandIds.RecordingStatus.Name,
        UcliCommandIds.RecordingStop.Name,
        UcliCommandIds.PlayStatus.Name,
        UcliCommandIds.PlayEnter.Name,
        UcliCommandIds.PlayExit.Name,
    };

    private static readonly string SupportedTimeoutCommandsDescription = string.Join(
        ", ",
        SupportedTimeoutCommands.OrderBy(static value => value, StringComparer.Ordinal));

    /// <summary> Compiles a strictly-valid configuration document into a shared snapshot. </summary>
    public UcliConfigContractCompilationResult Compile (JsonElement root, string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            throw new ArgumentException("Source path must not be empty.", nameof(sourcePath));
        }
        var diagnostics = new List<UcliConfigContractDiagnostic>();
        if (root.ValueKind != JsonValueKind.Object)
        {
            Add(diagnostics, "config.schema.rootTypeMismatch", null, sourcePath, "Config JSON root must be an object.");
            return UcliConfigContractCompilationResult.Failure(diagnostics);
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject())
        {
            var propertyName = UcliConfigContractDiagnostic.FormatFragment(property.Name);
            if (!seen.Add(property.Name))
            {
                if (!Add(diagnostics, "config.schema.duplicateProperty", propertyName, sourcePath, $"Config JSON contains duplicate property: {propertyName}."))
                {
                    return UcliConfigContractCompilationResult.Failure(diagnostics);
                }
            }

            if (!AllowedProperties.Contains(property.Name)
                && !Add(diagnostics, "config.schema.unknownProperty", propertyName, sourcePath, $"Config contains unknown property: {propertyName}."))
            {
                return UcliConfigContractCompilationResult.Failure(diagnostics);
            }
        }

        var schemaVersion = ReadRequiredInt32(root, UcliConfigJsonPropertyNames.SchemaVersion, sourcePath, diagnostics);
        var operationPolicyText = ReadRequiredString(root, UcliConfigJsonPropertyNames.OperationPolicy, sourcePath, diagnostics);
        var planTokenModeText = ReadRequiredString(root, UcliConfigJsonPropertyNames.PlanTokenMode, sourcePath, diagnostics);
        var readIndexModeText = ReadOptionalString(root, UcliConfigJsonPropertyNames.ReadIndexDefaultMode, sourcePath, diagnostics);
        var allowlist = ReadRequiredStringArray(root, UcliConfigJsonPropertyNames.OperationAllowlist, sourcePath, diagnostics);
        var evalEnabled = ReadOptionalBoolean(root, UcliConfigJsonPropertyNames.EvalEnabled, sourcePath, diagnostics);
        var defaultTimeout = ReadOptionalInt32(root, UcliConfigJsonPropertyNames.IpcDefaultTimeoutMilliseconds, sourcePath, diagnostics);
        var timeouts = ReadTimeouts(root, sourcePath, diagnostics);
        var presets = ReadProgramPresets(root, sourcePath, diagnostics);
        if (diagnostics.Count > 0)
        {
            return UcliConfigContractCompilationResult.Failure(diagnostics);
        }

        ValidateSchemaVersion(schemaVersion!.Value, sourcePath, diagnostics);
        var operationPolicy = Parse<OperationPolicy>(operationPolicyText!, UcliConfigJsonPropertyNames.OperationPolicy, sourcePath, diagnostics);
        var planTokenMode = Parse<PlanTokenMode>(planTokenModeText!, UcliConfigJsonPropertyNames.PlanTokenMode, sourcePath, diagnostics);
        var readIndexMode = Parse<ReadIndexMode>(readIndexModeText ?? "requireFresh", UcliConfigJsonPropertyNames.ReadIndexDefaultMode, sourcePath, diagnostics);
        var normalizedAllowlist = NormalizeAllowlist(allowlist!, sourcePath, diagnostics);
        var normalizedTimeouts = timeouts is null
            ? null
            : NormalizeTimeouts(timeouts, sourcePath, diagnostics);
        var normalizedPresets = NormalizeProgramPresets(presets, sourcePath, diagnostics);
        var requiredProgramPresets = ReadRequiredProgramPresets(root);
        var effectiveDefaultTimeout = defaultTimeout ?? DefaultIpcTimeoutMilliseconds;
        if (effectiveDefaultTimeout <= 0)
        {
            Add(diagnostics, "config.semantic.invalidTimeout", UcliConfigJsonPropertyNames.IpcDefaultTimeoutMilliseconds, sourcePath, $"Config {UcliConfigJsonPropertyNames.IpcDefaultTimeoutMilliseconds} is invalid: {effectiveDefaultTimeout}.");
        }

        if (diagnostics.Count > 0)
        {
            return UcliConfigContractCompilationResult.Failure(diagnostics);
        }

        return UcliConfigContractCompilationResult.Success(new UcliConfigContractSnapshot(
            schemaVersion.Value,
            operationPolicy,
            planTokenMode,
            readIndexMode,
            normalizedAllowlist,
            evalEnabled ?? false,
            effectiveDefaultTimeout,
            normalizedTimeouts,
            normalizedPresets,
            requiredProgramPresets));
    }

    private static int? ReadRequiredInt32 (JsonElement root, string name, string sourcePath, List<UcliConfigContractDiagnostic> diagnostics)
    {
        if (!root.TryGetProperty(name, out var value))
        {
            Add(diagnostics, "config.schema.missingProperty", name, sourcePath, $"Config JSON is missing required property: {name}.");
            return null;
        }

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var parsed))
        {
            Add(diagnostics, "config.schema.propertyTypeMismatch", name, sourcePath, $"Config JSON property type is invalid: {name}.");
            return null;
        }

        return parsed;
    }

    private static IReadOnlyList<string> ReadRequiredProgramPresets (JsonElement root)
    {
        if (!root.TryGetProperty(UcliConfigJsonPropertyNames.WorkCompletion, out var work)
            || !work.TryGetProperty(UcliConfigJsonPropertyNames.RequiredProgramPresets, out var values)
            || values.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }
        return values.EnumerateArray().Where(static value => value.ValueKind == JsonValueKind.String).Select(static value => value.GetString()!).ToArray();
    }

    private static string? ReadRequiredString (JsonElement root, string name, string sourcePath, List<UcliConfigContractDiagnostic> diagnostics)
    {
        if (!root.TryGetProperty(name, out var value))
        {
            Add(diagnostics, "config.schema.missingProperty", name, sourcePath, $"Config JSON is missing required property: {name}.");
            return null;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            Add(diagnostics, "config.schema.propertyTypeMismatch", name, sourcePath, $"Config JSON property type is invalid: {name}.");
            return null;
        }

        return value.GetString() ?? string.Empty;
    }

    private static string? ReadOptionalString (JsonElement root, string name, string sourcePath, List<UcliConfigContractDiagnostic> diagnostics)
    {
        if (!root.TryGetProperty(name, out var value))
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            Add(diagnostics, "config.schema.propertyTypeMismatch", name, sourcePath, $"Config JSON property type is invalid: {name}.");
            return null;
        }

        return value.GetString();
    }

    private static bool? ReadOptionalBoolean (JsonElement root, string name, string sourcePath, List<UcliConfigContractDiagnostic> diagnostics)
    {
        if (!root.TryGetProperty(name, out var value))
        {
            return null;
        }

        if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            Add(diagnostics, "config.schema.propertyTypeMismatch", name, sourcePath, $"Config JSON property type is invalid: {name}.");
            return null;
        }

        return value.GetBoolean();
    }

    private static int? ReadOptionalInt32 (JsonElement root, string name, string sourcePath, List<UcliConfigContractDiagnostic> diagnostics)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var parsed))
        {
            Add(diagnostics, "config.schema.propertyTypeMismatch", name, sourcePath, $"Config JSON property type is invalid: {name}.");
            return null;
        }

        return parsed;
    }

    private static string[]? ReadRequiredStringArray (JsonElement root, string name, string sourcePath, List<UcliConfigContractDiagnostic> diagnostics)
    {
        if (!root.TryGetProperty(name, out var value))
        {
            Add(diagnostics, "config.schema.missingProperty", name, sourcePath, $"Config JSON is missing required property: {name}.");
            return null;
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            Add(diagnostics, "config.schema.propertyTypeMismatch", name, sourcePath, $"Config JSON property type is invalid: {name}.");
            return null;
        }

        var values = new List<string>();
        var index = 0;
        foreach (var element in value.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.String)
            {
                var path = $"{name}[{index}]";
                if (!Add(diagnostics, "config.schema.arrayElementTypeMismatch", path, sourcePath, $"Config JSON array element type is invalid: {path}."))
                {
                    break;
                }
            }
            else
            {
                values.Add(element.GetString() ?? string.Empty);
            }

            index++;
        }

        return values.ToArray();
    }

    private static Dictionary<string, int?>? ReadTimeouts (JsonElement root, string sourcePath, List<UcliConfigContractDiagnostic> diagnostics)
    {
        var name = UcliConfigJsonPropertyNames.IpcTimeoutMillisecondsByCommand;
        if (!root.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            Add(diagnostics, "config.schema.propertyTypeMismatch", name, sourcePath, $"Config JSON property type is invalid: {name}.");
            return null;
        }

        var result = new Dictionary<string, int?>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in value.EnumerateObject())
        {
            var key = UcliConfigContractDiagnostic.FormatFragment(entry.Name);
            var path = $"{name}.{key}";
            if (!seen.Add(entry.Name))
            {
                if (!Add(diagnostics, "config.schema.duplicateProperty", path, sourcePath, $"Config JSON contains duplicate property: {path}."))
                {
                    break;
                }

                continue;
            }

            if (entry.Value.ValueKind == JsonValueKind.Null)
            {
                result[entry.Name] = null;
            }
            else if (entry.Value.ValueKind == JsonValueKind.Number && entry.Value.TryGetInt32(out var timeout))
            {
                result[entry.Name] = timeout;
            }
            else if (!Add(diagnostics, "config.schema.objectPropertyTypeMismatch", path, sourcePath, $"Config JSON object property type is invalid: {path}."))
            {
                break;
            }
        }

        return result;
    }

    private static Dictionary<string, UcliConfigContractProgramPreset>? ReadProgramPresets (JsonElement root, string sourcePath, List<UcliConfigContractDiagnostic> diagnostics)
    {
        var name = UcliConfigJsonPropertyNames.ProgramPresets;
        if (!root.TryGetProperty(name, out var value))
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            Add(diagnostics, "config.schema.propertyTypeMismatch", name, sourcePath, $"Config JSON property type is invalid: {name}.");
            return null;
        }

        var result = new Dictionary<string, UcliConfigContractProgramPreset>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in value.EnumerateObject())
        {
            var path = $"{name}.{UcliConfigContractDiagnostic.FormatFragment(entry.Name)}";
            if (!seen.Add(entry.Name))
            {
                if (!Add(diagnostics, "config.schema.duplicateProperty", path, sourcePath, $"Config JSON contains duplicate property: {path}."))
                {
                    break;
                }

                continue;
            }

            if (entry.Value.ValueKind != JsonValueKind.Object)
            {
                if (!Add(diagnostics, "config.schema.objectPropertyTypeMismatch", path, sourcePath, $"Config JSON object property type is invalid: {path}."))
                {
                    break;
                }

                continue;
            }

            var fields = new HashSet<string>(StringComparer.Ordinal);
            foreach (var field in entry.Value.EnumerateObject())
            {
                var fieldPath = $"{path}.{UcliConfigContractDiagnostic.FormatFragment(field.Name)}";
                if (!fields.Add(field.Name))
                {
                    Add(diagnostics, "config.schema.duplicateProperty", fieldPath, sourcePath, $"Config JSON contains duplicate property: {fieldPath}.");
                }
                else if (field.Name is not ("description" or "programPath"))
                {
                    Add(diagnostics, "config.schema.unknownProperty", fieldPath, sourcePath, $"Config contains unknown property: {fieldPath}.");
                }
            }

            if (!entry.Value.TryGetProperty("description", out var description) || description.ValueKind != JsonValueKind.String)
            {
                Add(diagnostics, "config.schema.propertyTypeMismatch", $"{path}.description", sourcePath, $"Config JSON property type is invalid: {path}.description.");
                continue;
            }

            if (!entry.Value.TryGetProperty("programPath", out var programPath) || programPath.ValueKind != JsonValueKind.String)
            {
                Add(diagnostics, "config.schema.propertyTypeMismatch", $"{path}.programPath", sourcePath, $"Config JSON property type is invalid: {path}.programPath.");
                continue;
            }

            result[entry.Name] = new UcliConfigContractProgramPreset(description.GetString() ?? string.Empty, programPath.GetString() ?? string.Empty);
        }

        return result;
    }

    private static void ValidateSchemaVersion (int value, string sourcePath, List<UcliConfigContractDiagnostic> diagnostics)
    {
        if (value != CurrentSchemaVersion)
        {
            Add(diagnostics, "config.semantic.unsupportedSchemaVersion", UcliConfigJsonPropertyNames.SchemaVersion, sourcePath, $"Config schemaVersion must be {CurrentSchemaVersion}. Actual: {UcliConfigContractDiagnostic.FormatFragment(value.ToString())}.");
        }
    }

    private static TEnum Parse<TEnum> (string value, string path, string sourcePath, List<UcliConfigContractDiagnostic> diagnostics)
        where TEnum : struct, Enum
    {
        if (VocabularyInputParser.TryParseIgnoreCase<TEnum>(value, out var parsed))
        {
            return parsed;
        }

        Add(diagnostics, "config.semantic.unsupportedLiteral", path, sourcePath, $"Config {path} is invalid: {UcliConfigContractDiagnostic.FormatFragment(value)}.");
        return default;
    }

    private static string[] NormalizeAllowlist (IReadOnlyList<string> source, string sourcePath, List<UcliConfigContractDiagnostic> diagnostics)
    {
        var result = new List<string>(source.Count);
        for (var i = 0; i < source.Count; i++)
        {
            var path = $"{UcliConfigJsonPropertyNames.OperationAllowlist}[{i}]";
            if (!StringValueNormalizer.TryTrimToNonEmpty(source[i], out var pattern))
            {
                if (!Add(diagnostics, "config.semantic.emptyAllowlistPattern", path, sourcePath, "Config operationAllowlist contains an empty pattern."))
                {
                    break;
                }

                continue;
            }

            if (!RegexPatternUtilities.TryValidatePattern(pattern, out var error))
            {
                if (!Add(diagnostics, "config.semantic.invalidRegexPattern", path, sourcePath, $"Config operationAllowlist contains an invalid regex pattern: {UcliConfigContractDiagnostic.FormatFragment(pattern)}. {UcliConfigContractDiagnostic.FormatFragment(error)}"))
                {
                    break;
                }

                continue;
            }

            result.Add(pattern);
        }

        return result.ToArray();
    }

    private static Dictionary<string, int?> NormalizeTimeouts (IReadOnlyDictionary<string, int?>? source, string sourcePath, List<UcliConfigContractDiagnostic> diagnostics)
    {
        var result = new Dictionary<string, int?>(StringComparer.Ordinal);
        if (source is null)
        {
            return result;
        }

        foreach (var entry in source)
        {
            var key = UcliConfigContractDiagnostic.FormatFragment(entry.Key);
            var path = $"{UcliConfigJsonPropertyNames.IpcTimeoutMillisecondsByCommand}.{key}";
            if (!SupportedTimeoutCommands.Contains(entry.Key))
            {
                if (!Add(diagnostics, "config.semantic.unsupportedTimeoutCommand", path, sourcePath, $"Config ipcTimeoutMillisecondsByCommand contains unsupported command key: {key}. Supported: {SupportedTimeoutCommandsDescription}."))
                {
                    break;
                }

                continue;
            }

            if (entry.Value is <= 0)
            {
                if (!Add(diagnostics, "config.semantic.invalidTimeout", path, sourcePath, $"Config {path} is invalid: {entry.Value}."))
                {
                    break;
                }

                continue;
            }

            result[entry.Key] = entry.Value;
        }

        return result;
    }

    private static Dictionary<string, UcliConfigContractProgramPreset> NormalizeProgramPresets (IReadOnlyDictionary<string, UcliConfigContractProgramPreset>? source, string sourcePath, List<UcliConfigContractDiagnostic> diagnostics)
    {
        var result = new Dictionary<string, UcliConfigContractProgramPreset>(StringComparer.Ordinal);
        if (source is null)
        {
            return result;
        }

        foreach (var entry in source)
        {
            var path = $"{UcliConfigJsonPropertyNames.ProgramPresets}.{entry.Key}";
            if (!IsValidPresetId(entry.Key))
            {
                Add(diagnostics, "config.semantic.invalidProgramPresetId", path, sourcePath, "Config Program Preset ID is invalid.");
                continue;
            }

            if (entry.Value.Description is not { Length: >= 1 and <= 1024 } || string.IsNullOrWhiteSpace(entry.Value.Description))
            {
                Add(diagnostics, "config.semantic.invalidProgramPresetDescription", $"{path}.description", sourcePath, "Config Program Preset description must contain 1 through 1024 characters.");
                continue;
            }

            if (!IsValidProgramPath(entry.Value.ProgramPath))
            {
                Add(diagnostics, "config.semantic.invalidProgramPresetPath", $"{path}.programPath", sourcePath, "Config Program Preset programPath must be a relative .json path without dot segments.");
                continue;
            }

            result.Add(entry.Key, entry.Value);
        }

        return result;
    }

    private static bool IsValidPresetId (string value)
    {
        return value.Length is >= 1 and <= 128
            && System.Text.RegularExpressions.Regex.IsMatch(value, "^[a-z][a-z0-9]*(?:[.-][a-z0-9]+)*$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    }

    private static bool IsValidProgramPath (string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.EndsWith(".json", StringComparison.Ordinal)
            && !Path.IsPathRooted(value)
            && !value.Contains('\\')
            && value.Split('/', StringSplitOptions.None).All(static segment => segment is not ("" or "." or ".."));
    }

    private static bool Add (List<UcliConfigContractDiagnostic> diagnostics, string code, string? propertyPath, string sourcePath, string message)
    {
        if (diagnostics.Count < MaxDiagnostics)
        {
            diagnostics.Add(new UcliConfigContractDiagnostic(code, propertyPath, sourcePath, message));
            return true;
        }

        if (diagnostics.Count == MaxDiagnostics)
        {
            diagnostics.Add(new UcliConfigContractDiagnostic(OmittedDiagnosticsCode, null, sourcePath, OmittedDiagnosticsMessage));
        }

        return false;
    }
}

/// <summary> Represents one fully validated configuration snapshot shared across runtimes. </summary>
internal sealed record UcliConfigContractSnapshot (
    int SchemaVersion,
    OperationPolicy OperationPolicy,
    PlanTokenMode PlanTokenMode,
    ReadIndexMode ReadIndexDefaultMode,
    IReadOnlyList<string> OperationAllowlist,
    bool EvalEnabled,
    int IpcDefaultTimeoutMilliseconds,
    IReadOnlyDictionary<string, int?>? IpcTimeoutMillisecondsByCommand,
    IReadOnlyDictionary<string, UcliConfigContractProgramPreset> ProgramPresets,
    IReadOnlyList<string> RequiredProgramPresets);

/// <summary> Represents one Program Preset registration in the shared configuration snapshot. </summary>
internal sealed record UcliConfigContractProgramPreset (string Description, string ProgramPath);

/// <summary> Represents one safe diagnostic emitted by the shared configuration compiler. </summary>
internal sealed class UcliConfigContractDiagnostic
{
    private const int MaxTextLength = 512;
    private const string TruncatedSuffix = "...";

    public UcliConfigContractDiagnostic (string code, string? propertyPath, string? sourcePath, string message)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Diagnostic code must not be empty.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Diagnostic message must not be empty.", nameof(message));
        }
        Code = SanitizeRequired(code);
        PropertyPath = SanitizeOptional(propertyPath);
        SourcePath = SanitizeOptional(sourcePath);
        Message = SanitizeRequired(message);
    }

    public string Code { get; }

    public string? PropertyPath { get; }

    public string? SourcePath { get; }

    public string Message { get; }

    public static string FormatFragment (string? value) => SanitizeRequired(value ?? "<null>");

    private static string SanitizeRequired (string value) => Limit(Escape(value));

    private static string? SanitizeOptional (string? value) => value is null ? null : Limit(Escape(value));

    private static string Escape (string value)
    {
        System.Text.StringBuilder? builder = null;
        for (var index = 0; index < value.Length; index++)
        {
            var scalarLength = GetScalarLength(value, index);
            var category = char.GetUnicodeCategory(value, index);
            if (category is not (System.Globalization.UnicodeCategory.Control
                or System.Globalization.UnicodeCategory.LineSeparator
                or System.Globalization.UnicodeCategory.ParagraphSeparator
                or System.Globalization.UnicodeCategory.Format))
            {
                if (builder is not null)
                {
                    builder.Append(value, index, scalarLength);
                }

                if (scalarLength > 1)
                {
                    index++;
                }

                continue;
            }

            builder ??= new System.Text.StringBuilder(value.Length + 8);
            if (builder.Length == 0 && index > 0)
            {
                builder.Append(value, 0, index);
            }

            for (var offset = 0; offset < scalarLength; offset++)
            {
                builder.Append("\\u").Append(((int)value[index + offset]).ToString("X4", System.Globalization.CultureInfo.InvariantCulture));
            }

            if (scalarLength > 1)
            {
                index++;
            }
        }

        return builder?.ToString() ?? value;
    }

    private static int GetScalarLength (string value, int index)
    {
        return char.IsHighSurrogate(value[index])
            && index + 1 < value.Length
            && char.IsLowSurrogate(value[index + 1])
            ? 2
            : 1;
    }

    private static string Limit (string value) => value.Length <= MaxTextLength ? value : value[..(MaxTextLength - TruncatedSuffix.Length)] + TruncatedSuffix;
}

/// <summary> Represents the shared configuration compilation outcome. </summary>
internal sealed record UcliConfigContractCompilationResult (UcliConfigContractSnapshot? Snapshot, IReadOnlyList<UcliConfigContractDiagnostic> Diagnostics)
{
    public bool IsSuccess => Snapshot is not null && Diagnostics.Count == 0;

    public static UcliConfigContractCompilationResult Success (UcliConfigContractSnapshot snapshot)
    {
        if (snapshot is null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }
        return new UcliConfigContractCompilationResult(snapshot, Array.Empty<UcliConfigContractDiagnostic>());
    }

    public static UcliConfigContractCompilationResult Failure (IReadOnlyList<UcliConfigContractDiagnostic> diagnostics)
    {
        if (diagnostics is null)
        {
            throw new ArgumentNullException(nameof(diagnostics));
        }
        if (diagnostics.Count == 0)
        {
            throw new ArgumentException("Failure diagnostics must not be empty.", nameof(diagnostics));
        }

        return new UcliConfigContractCompilationResult(null, diagnostics.ToArray());
    }
}
