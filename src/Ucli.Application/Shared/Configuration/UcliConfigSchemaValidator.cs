using System.Text.Json;
using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Application.Shared.Configuration;

/// <summary> Validates JSON shape for <c>.ucli/config.json</c> and reads raw config values. </summary>
internal sealed class UcliConfigSchemaValidator
{
    private const string RootTypeMismatchCode = "config.schema.rootTypeMismatch";
    private const string UnknownPropertyCode = "config.schema.unknownProperty";
    private const string MissingPropertyCode = "config.schema.missingProperty";
    private const string PropertyTypeMismatchCode = "config.schema.propertyTypeMismatch";
    private const string ArrayElementTypeMismatchCode = "config.schema.arrayElementTypeMismatch";
    private const string ObjectPropertyTypeMismatchCode = "config.schema.objectPropertyTypeMismatch";
    private const string DuplicatePropertyCode = "config.schema.duplicateProperty";

    private static readonly HashSet<string> StrictAllowedProperties = new(StringComparer.Ordinal)
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
    };

    /// <summary> Validates a config JSON root and reads raw config values when validation succeeds. </summary>
    /// <param name="root"> The config JSON root element. </param>
    /// <param name="sourcePath"> The source config path used in diagnostics. </param>
    /// <returns> The schema validation result. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="sourcePath" /> is empty. </exception>
    public UcliConfigSchemaValidationResult Validate (
        JsonElement root,
        string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        var diagnostics = new List<UcliConfigDiagnostic>();
        if (root.ValueKind != JsonValueKind.Object)
        {
            AddDiagnostic(diagnostics, CreateDiagnostic(
                RootTypeMismatchCode,
                propertyPath: null,
                sourcePath,
                "Config JSON root must be an object."));
            return UcliConfigSchemaValidationResult.Failure(diagnostics);
        }

        AddObjectPropertyDiagnostics(root, sourcePath, diagnostics);
        if (UcliConfigDiagnosticList.HasReachedLimit(diagnostics))
        {
            return UcliConfigSchemaValidationResult.Failure(diagnostics);
        }

        var schemaVersion = ReadRequiredInt32(
            root,
            UcliConfigJsonPropertyNames.SchemaVersion,
            sourcePath,
            diagnostics);
        var operationPolicy = ReadRequiredString(
            root,
            UcliConfigJsonPropertyNames.OperationPolicy,
            sourcePath,
            diagnostics);
        var planTokenMode = ReadRequiredString(
            root,
            UcliConfigJsonPropertyNames.PlanTokenMode,
            sourcePath,
            diagnostics);
        var readIndexDefaultMode = ReadOptionalString(
            root,
            UcliConfigJsonPropertyNames.ReadIndexDefaultMode,
            sourcePath,
            diagnostics);
        var operationAllowlist = ReadRequiredStringArray(
            root,
            UcliConfigJsonPropertyNames.OperationAllowlist,
            sourcePath,
            diagnostics);
        var evalEnabled = ReadOptionalBoolean(
            root,
            UcliConfigJsonPropertyNames.EvalEnabled,
            sourcePath,
            diagnostics);
        var ipcDefaultTimeoutMilliseconds = ReadOptionalNullableInt32(
            root,
            UcliConfigJsonPropertyNames.IpcDefaultTimeoutMilliseconds,
            sourcePath,
            diagnostics);
        var ipcTimeoutMillisecondsByCommand = ReadOptionalInt32Dictionary(
            root,
            UcliConfigJsonPropertyNames.IpcTimeoutMillisecondsByCommand,
            sourcePath,
            diagnostics);
        var programPresets = ReadOptionalProgramPresetDictionary(
            root,
            UcliConfigJsonPropertyNames.ProgramPresets,
            sourcePath,
            diagnostics);

        if (diagnostics.Count > 0)
        {
            return UcliConfigSchemaValidationResult.Failure(diagnostics);
        }

        return UcliConfigSchemaValidationResult.Success(new UcliConfigJsonRawDocument(
            SchemaVersion: schemaVersion!.Value,
            OperationPolicy: operationPolicy!,
            PlanTokenMode: planTokenMode!,
            ReadIndexDefaultMode: readIndexDefaultMode,
            OperationAllowlist: operationAllowlist!,
            EvalEnabled: evalEnabled,
            IpcDefaultTimeoutMilliseconds: ipcDefaultTimeoutMilliseconds,
            IpcTimeoutMillisecondsByCommand: ipcTimeoutMillisecondsByCommand,
            ProgramPresets: programPresets));
    }

    private static void AddObjectPropertyDiagnostics (
        JsonElement root,
        string sourcePath,
        List<UcliConfigDiagnostic> diagnostics)
    {
        var seenPropertyNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject())
        {
            var propertyName = UcliConfigDiagnostic.FormatFragment(property.Name);
            if (!seenPropertyNames.Add(property.Name))
            {
                if (!AddDiagnostic(diagnostics, CreateDuplicatePropertyDiagnostic(propertyName, sourcePath)))
                {
                    return;
                }
            }

            if (StrictAllowedProperties.Contains(property.Name))
            {
                continue;
            }

            if (!AddDiagnostic(diagnostics, CreateDiagnostic(
                UnknownPropertyCode,
                propertyName,
                sourcePath,
                $"Config contains unknown property: {propertyName}.")))
            {
                return;
            }
        }
    }

    private static int? ReadRequiredInt32 (
        JsonElement root,
        string propertyName,
        string sourcePath,
        List<UcliConfigDiagnostic> diagnostics)
    {
        if (!root.TryGetProperty(propertyName, out var property))
        {
            AddDiagnostic(diagnostics, CreateMissingPropertyDiagnostic(propertyName, sourcePath));
            return null;
        }

        if (property.ValueKind != JsonValueKind.Number || !property.TryGetInt32(out var value))
        {
            AddDiagnostic(diagnostics, CreatePropertyTypeMismatchDiagnostic(propertyName, sourcePath));
            return null;
        }

        return value;
    }

    private static string? ReadRequiredString (
        JsonElement root,
        string propertyName,
        string sourcePath,
        List<UcliConfigDiagnostic> diagnostics)
    {
        if (!root.TryGetProperty(propertyName, out var property))
        {
            AddDiagnostic(diagnostics, CreateMissingPropertyDiagnostic(propertyName, sourcePath));
            return null;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            AddDiagnostic(diagnostics, CreatePropertyTypeMismatchDiagnostic(propertyName, sourcePath));
            return null;
        }

        return property.GetString() ?? string.Empty;
    }

    private static string? ReadOptionalString (
        JsonElement root,
        string propertyName,
        string sourcePath,
        List<UcliConfigDiagnostic> diagnostics)
    {
        if (!root.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            AddDiagnostic(diagnostics, CreatePropertyTypeMismatchDiagnostic(propertyName, sourcePath));
            return null;
        }

        return property.GetString();
    }

    private static bool? ReadOptionalBoolean (
        JsonElement root,
        string propertyName,
        string sourcePath,
        List<UcliConfigDiagnostic> diagnostics)
    {
        if (!root.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            AddDiagnostic(diagnostics, CreatePropertyTypeMismatchDiagnostic(propertyName, sourcePath));
            return null;
        }

        return property.GetBoolean();
    }

    private static int? ReadOptionalNullableInt32 (
        JsonElement root,
        string propertyName,
        string sourcePath,
        List<UcliConfigDiagnostic> diagnostics)
    {
        if (!root.TryGetProperty(propertyName, out var property)
            || property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (property.ValueKind != JsonValueKind.Number || !property.TryGetInt32(out var value))
        {
            AddDiagnostic(diagnostics, CreatePropertyTypeMismatchDiagnostic(propertyName, sourcePath));
            return null;
        }

        return value;
    }

    private static string[]? ReadRequiredStringArray (
        JsonElement root,
        string propertyName,
        string sourcePath,
        List<UcliConfigDiagnostic> diagnostics)
    {
        if (!root.TryGetProperty(propertyName, out var property))
        {
            AddDiagnostic(diagnostics, CreateMissingPropertyDiagnostic(propertyName, sourcePath));
            return null;
        }

        if (property.ValueKind != JsonValueKind.Array)
        {
            AddDiagnostic(diagnostics, CreatePropertyTypeMismatchDiagnostic(propertyName, sourcePath));
            return null;
        }

        var values = new List<string>();
        var index = 0;
        foreach (var element in property.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.String)
            {
                var propertyPath = $"{propertyName}[{index}]";
                if (!AddDiagnostic(diagnostics, CreateDiagnostic(
                    ArrayElementTypeMismatchCode,
                    propertyPath,
                    sourcePath,
                    $"Config JSON array element type is invalid: {propertyPath}.")))
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

    private static Dictionary<string, int?>? ReadOptionalInt32Dictionary (
        JsonElement root,
        string propertyName,
        string sourcePath,
        List<UcliConfigDiagnostic> diagnostics)
    {
        if (!root.TryGetProperty(propertyName, out var property)
            || property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (property.ValueKind != JsonValueKind.Object)
        {
            AddDiagnostic(diagnostics, CreatePropertyTypeMismatchDiagnostic(propertyName, sourcePath));
            return null;
        }

        var values = new Dictionary<string, int?>(StringComparer.Ordinal);
        var seenPropertyNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in property.EnumerateObject())
        {
            var entryName = UcliConfigDiagnostic.FormatFragment(entry.Name);
            var propertyPath = $"{propertyName}.{entryName}";
            if (!seenPropertyNames.Add(entry.Name))
            {
                if (!AddDiagnostic(diagnostics, CreateDuplicatePropertyDiagnostic(propertyPath, sourcePath)))
                {
                    break;
                }

                continue;
            }

            if (entry.Value.ValueKind == JsonValueKind.Null)
            {
                values[entry.Name] = null;
                continue;
            }

            if (entry.Value.ValueKind != JsonValueKind.Number || !entry.Value.TryGetInt32(out var timeoutValue))
            {
                if (!AddDiagnostic(diagnostics, CreateDiagnostic(
                    ObjectPropertyTypeMismatchCode,
                    propertyPath,
                    sourcePath,
                    $"Config JSON object property type is invalid: {propertyPath}.")))
                {
                    break;
                }

                continue;
            }

            values[entry.Name] = timeoutValue;
        }

        return values;
    }

    private static Dictionary<string, UcliProgramPresetDocument>? ReadOptionalProgramPresetDictionary (
        JsonElement root,
        string propertyName,
        string sourcePath,
        List<UcliConfigDiagnostic> diagnostics)
    {
        if (!root.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind != JsonValueKind.Object)
        {
            AddDiagnostic(diagnostics, CreatePropertyTypeMismatchDiagnostic(propertyName, sourcePath));
            return null;
        }

        var values = new Dictionary<string, UcliProgramPresetDocument>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in property.EnumerateObject())
        {
            var entryPath = $"{propertyName}.{UcliConfigDiagnostic.FormatFragment(entry.Name)}";
            if (!seen.Add(entry.Name))
            {
                if (!AddDiagnostic(diagnostics, CreateDuplicatePropertyDiagnostic(entryPath, sourcePath)))
                {
                    break;
                }

                continue;
            }

            if (entry.Value.ValueKind != JsonValueKind.Object)
            {
                if (!AddDiagnostic(diagnostics, CreateDiagnostic(ObjectPropertyTypeMismatchCode, entryPath, sourcePath, $"Config JSON object property type is invalid: {entryPath}.")))
                {
                    break;
                }

                continue;
            }

            var registrationProperties = new HashSet<string>(StringComparer.Ordinal);
            foreach (var registrationProperty in entry.Value.EnumerateObject())
            {
                var registrationPath = $"{entryPath}.{UcliConfigDiagnostic.FormatFragment(registrationProperty.Name)}";
                if (!registrationProperties.Add(registrationProperty.Name))
                {
                    AddDiagnostic(diagnostics, CreateDuplicatePropertyDiagnostic(registrationPath, sourcePath));
                }
                else if (registrationProperty.Name is not ("description" or "programPath"))
                {
                    AddDiagnostic(diagnostics, CreateDiagnostic(UnknownPropertyCode, registrationPath, sourcePath, $"Config contains unknown property: {registrationPath}."));
                }
            }

            if (!entry.Value.TryGetProperty("description", out var description) || description.ValueKind != JsonValueKind.String)
            {
                AddDiagnostic(diagnostics, CreateDiagnostic(PropertyTypeMismatchCode, $"{entryPath}.description", sourcePath, $"Config JSON property type is invalid: {entryPath}.description."));
                continue;
            }

            if (!entry.Value.TryGetProperty("programPath", out var programPath) || programPath.ValueKind != JsonValueKind.String)
            {
                AddDiagnostic(diagnostics, CreateDiagnostic(PropertyTypeMismatchCode, $"{entryPath}.programPath", sourcePath, $"Config JSON property type is invalid: {entryPath}.programPath."));
                continue;
            }

            values.Add(entry.Name, new UcliProgramPresetDocument(description.GetString() ?? string.Empty, programPath.GetString() ?? string.Empty));
        }

        return values;
    }

    private static UcliConfigDiagnostic CreateMissingPropertyDiagnostic (
        string propertyName,
        string sourcePath)
    {
        return CreateDiagnostic(
            MissingPropertyCode,
            propertyName,
            sourcePath,
            $"Config JSON is missing required property: {propertyName}.");
    }

    private static UcliConfigDiagnostic CreatePropertyTypeMismatchDiagnostic (
        string propertyName,
        string sourcePath)
    {
        return CreateDiagnostic(
            PropertyTypeMismatchCode,
            propertyName,
            sourcePath,
            $"Config JSON property type is invalid: {propertyName}.");
    }

    private static UcliConfigDiagnostic CreateDuplicatePropertyDiagnostic (
        string propertyPath,
        string sourcePath)
    {
        return CreateDiagnostic(
            DuplicatePropertyCode,
            propertyPath,
            sourcePath,
            $"Config JSON contains duplicate property: {propertyPath}.");
    }

    private static UcliConfigDiagnostic CreateDiagnostic (
        string code,
        string? propertyPath,
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
}
