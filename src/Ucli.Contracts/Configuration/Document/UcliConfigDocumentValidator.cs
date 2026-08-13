using System.Text.Json;
using System.Text.RegularExpressions;

namespace MackySoft.Ucli.Contracts.Configuration;

/// <summary> Validates the closed, portable portion of one <c>.ucli/config.json</c> document. </summary>
public static class UcliConfigDocumentValidator
{
    private const int MaximumAllowlistEntries = 64;
    private const int MaximumAllowlistPatternLength = 1024;
    private const int MaximumPresetIdLength = 128;
    private const int MaximumPresetDescriptionLength = 1024;
    private static readonly Regex PresetIdPattern = new(
        "\\A[a-z][a-z0-9]*(?:[.-][a-z0-9]+)*\\z",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    /// <summary>
    /// Validates JSON and produces the values used to calculate the Program effective-configuration digest.
    /// File-system and feature-specific resolution remain the responsibility of the caller.
    /// </summary>
    /// <param name="root"> The parsed config document root. </param>
    /// <param name="timeoutDefaults"> The command keys and defaults exposed by this distribution. </param>
    /// <param name="document"> The validated effective document. </param>
    /// <returns> <see langword="true" /> when the complete portable config contract is valid. </returns>
    public static bool TryValidate (
        JsonElement root,
        IReadOnlyDictionary<string, int> timeoutDefaults,
        out UcliConfigValidatedDocument? document)
    {
        if (timeoutDefaults is null)
        {
            throw new ArgumentNullException(nameof(timeoutDefaults));
        }
        document = null;
        if (root.ValueKind != JsonValueKind.Object || !HasOnly(root, RootProperties))
        {
            return false;
        }

        if (!TryGetInt32(root, "schemaVersion", out var schemaVersion) || schemaVersion != 1
            || !TryGetLiteral(root, "operationPolicy", "safe", "advanced", "dangerous", out var operationPolicy)
            || !TryGetLiteral(root, "planTokenMode", "optional", "required", out var planTokenMode)
            || !TryGetOptionalLiteral(root, "readIndexDefaultMode", "requireFresh", new[] { "disabled", "allowStale", "requireFresh" }, out var readIndexDefaultMode)
            || !TryReadAllowlist(root, out var operationAllowlist)
            || !TryReadOptionalPositiveInt(root, "ipcDefaultTimeoutMilliseconds", 3000, out var defaultTimeout)
            || !TryReadTimeouts(root, timeoutDefaults, defaultTimeout, out var timeouts)
            || !TryReadOptionalBoolean(root, "evalEnabled", false, out var evalEnabled)
            || !TryReadProgramPresets(root)
            || !TryReadWorkCompletion(root))
        {
            return false;
        }

        document = new UcliConfigValidatedDocument(
            schemaVersion,
            operationPolicy!,
            planTokenMode!,
            readIndexDefaultMode!,
            operationAllowlist!,
            defaultTimeout,
            timeouts!,
            evalEnabled);
        return true;
    }

    private static readonly HashSet<string> RootProperties = new(StringComparer.Ordinal)
    {
        "schemaVersion", "operationPolicy", "planTokenMode", "readIndexDefaultMode", "operationAllowlist",
        "ipcDefaultTimeoutMilliseconds", "ipcTimeoutMillisecondsByCommand", "evalEnabled", "programPresets", "workCompletion",
    };

    private static bool HasOnly (JsonElement owner, ISet<string> allowed)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in owner.EnumerateObject())
        {
            if (!names.Add(property.Name) || !allowed.Contains(property.Name))
            {
                return false;
            }
        }
        return true;
    }

    private static bool TryGetInt32 (JsonElement owner, string name, out int value)
    {
        value = default;
        return owner.TryGetProperty(name, out var property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt32(out value);
    }

    private static bool TryGetLiteral (JsonElement owner, string name, string first, string second, out string? value) =>
        TryGetLiteral(owner, name, first, second, third: null, out value);

    private static bool TryGetLiteral (JsonElement owner, string name, string first, string second, string? third, out string? value) =>
        TryGetLiteral(owner, name, first, second, third, fourth: null, out value);

    private static bool TryGetLiteral (JsonElement owner, string name, string first, string second, string? third, string? fourth, out string? value)
    {
        value = null;
        if (!owner.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }
        value = property.GetString();
        return value == first || value == second || value == third || value == fourth;
    }

    private static bool TryGetOptionalLiteral (JsonElement owner, string name, string fallback, IReadOnlyList<string> allowed, out string? value)
    {
        value = fallback;
        if (!owner.TryGetProperty(name, out var property))
        {
            return true;
        }
        if (property.ValueKind != JsonValueKind.String)
        {
            return false;
        }
        value = property.GetString();
        return value is not null && allowed.Contains(value);
    }

    private static bool TryReadAllowlist (JsonElement root, out string[]? values)
    {
        values = null;
        if (!root.TryGetProperty("operationAllowlist", out var property) || property.ValueKind != JsonValueKind.Array || property.GetArrayLength() > MaximumAllowlistEntries)
        {
            return false;
        }
        var result = new List<string>();
        foreach (var item in property.EnumerateArray())
        {
            var value = item.ValueKind == JsonValueKind.String ? item.GetString() : null;
            if (value is null || value.Length == 0 || value.Length > MaximumAllowlistPatternLength || HasInlineRegexOptions(value) || !TryCompileAllowlistRegex(value))
            {
                return false;
            }
            result.Add(value);
        }
        values = result.ToArray();
        return true;
    }

    private static bool HasInlineRegexOptions (string pattern)
    {
        for (var index = 0; index + 2 < pattern.Length; index++)
        {
            if (pattern[index] != '(' || pattern[index + 1] != '?')
            {
                continue;
            }
            for (var optionIndex = index + 2; optionIndex < pattern.Length; optionIndex++)
            {
                var character = pattern[optionIndex];
                if (character is 'i' or 'm' or 'n' or 's' or 'x')
                {
                    return true;
                }
                if (character is ':' or ')')
                {
                    break;
                }
                if (character != '-')
                {
                    break;
                }
            }
        }
        return false;
    }

    private static bool TryCompileAllowlistRegex (string pattern)
    {
        try
        {
            _ = new Regex(pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool TryReadOptionalPositiveInt (JsonElement root, string name, int fallback, out int value)
    {
        value = fallback;
        if (!root.TryGetProperty(name, out var property) || property.ValueKind == JsonValueKind.Null)
        {
            return true;
        }
        return property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out value) && value > 0;
    }

    private static bool TryReadTimeouts (JsonElement root, IReadOnlyDictionary<string, int> defaults, int defaultTimeout, out IReadOnlyDictionary<string, int>? values)
    {
        var result = new Dictionary<string, int>(defaults, StringComparer.Ordinal);
        if (!root.TryGetProperty("ipcTimeoutMillisecondsByCommand", out var property))
        {
            values = result;
            return true;
        }
        if (property.ValueKind != JsonValueKind.Object)
        {
            values = null;
            return false;
        }
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in property.EnumerateObject())
        {
            if (!names.Add(entry.Name) || !result.ContainsKey(entry.Name))
            {
                values = null;
                return false;
            }
            if (entry.Value.ValueKind == JsonValueKind.Null)
            {
                result[entry.Name] = defaultTimeout;
            }
            else if (entry.Value.ValueKind == JsonValueKind.Number && entry.Value.TryGetInt32(out var timeout) && timeout > 0)
            {
                result[entry.Name] = timeout;
            }
            else
            {
                values = null;
                return false;
            }
        }
        values = result;
        return true;
    }

    private static bool TryReadOptionalBoolean (JsonElement root, string name, bool fallback, out bool value)
    {
        value = fallback;
        if (!root.TryGetProperty(name, out var property))
        {
            return true;
        }
        if (property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return false;
        }
        value = property.GetBoolean();
        return true;
    }

    private static bool TryReadProgramPresets (JsonElement root)
    {
        if (!root.TryGetProperty("programPresets", out var property))
        {
            return true;
        }
        if (property.ValueKind != JsonValueKind.Object)
        {
            return false;
        }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in property.EnumerateObject())
        {
            if (!ids.Add(entry.Name) || !IsPresetId(entry.Name) || entry.Value.ValueKind != JsonValueKind.Object || !TryReadProgramPreset(entry.Value))
            {
                return false;
            }
        }
        return true;
    }

    private static bool TryReadProgramPreset (JsonElement value)
    {
        var properties = new HashSet<string>(StringComparer.Ordinal);
        string? description = null;
        string? programPath = null;
        foreach (var property in value.EnumerateObject())
        {
            if (!properties.Add(property.Name) || property.Name is not ("description" or "programPath") || property.Value.ValueKind != JsonValueKind.String)
            {
                return false;
            }
            if (property.Name == "description")
            {
                description = property.Value.GetString();
            }
            else
            {
                programPath = property.Value.GetString();
            }
        }
        return description is { Length: > 0 and <= MaximumPresetDescriptionLength } && IsProgramPath(programPath);
    }

    private static bool TryReadWorkCompletion (JsonElement root)
    {
        if (!root.TryGetProperty("workCompletion", out var property))
        {
            return true;
        }
        if (property.ValueKind != JsonValueKind.Object || !HasOnly(property, new HashSet<string>(StringComparer.Ordinal) { "requiredProgramPresets" }) || !property.TryGetProperty("requiredProgramPresets", out var presets) || presets.ValueKind != JsonValueKind.Array)
        {
            return false;
        }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in presets.EnumerateArray())
        {
            var id = item.ValueKind == JsonValueKind.String ? item.GetString() : null;
            if (id is null || !ids.Add(id) || !IsPresetId(id))
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsPresetId (string value) => value.Length is > 0 and <= MaximumPresetIdLength && PresetIdPattern.IsMatch(value);

    private static bool IsProgramPath (string? value)
    {
        if (string.IsNullOrEmpty(value) || !value.EndsWith(".json", StringComparison.Ordinal) || value[0] == '/' || value.Contains('\\'))
        {
            return false;
        }
        foreach (var segment in value.Split('/'))
        {
            if (segment.Length == 0 || segment is "." or "..")
            {
                return false;
            }
        }
        return true;
    }
}

/// <summary> Represents the portable effective configuration fixed for a Program execution. </summary>
public sealed record UcliConfigValidatedDocument (
    int SchemaVersion,
    string OperationPolicy,
    string PlanTokenMode,
    string ReadIndexDefaultMode,
    IReadOnlyList<string> OperationAllowlist,
    int IpcDefaultTimeoutMilliseconds,
    IReadOnlyDictionary<string, int> IpcTimeoutMillisecondsByCommand,
    bool EvalEnabled);
