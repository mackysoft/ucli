using System.Text.Json;
using MackySoft.Ucli.Contracts.Paths;
using MackySoft.Ucli.Foundation;

namespace MackySoft.Ucli.TestRun.Configuration;

/// <summary> Implements JSON profile loading for test-run configuration input. </summary>
internal sealed class TestRunProfileLoader : ITestRunProfileLoader
{
    private static readonly HashSet<string> AllowedProperties = new(StringComparer.Ordinal)
    {
        "schemaVersion",
        "projectPath",
        "unityVersion",
        "unityEditorPath",
        "testPlatform",
        "buildTarget",
        "testFilter",
        "testCategories",
        "assemblyNames",
        "testSettingsPath",
        "timeout",
    };

    /// <summary> Loads one profile JSON file from disk. </summary>
    /// <param name="profilePath"> The profile path value. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by caller. </param>
    /// <returns> A task that resolves to the profile load result. </returns>
    public async ValueTask<TestRunProfileLoadResult> Load (
        string profilePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(profilePath))
        {
            return TestRunProfileLoadResult.Failure(ExecutionError.InvalidArgument("profilePath is empty."));
        }

        var normalizedProfilePath = profilePath;
        try
        {
            normalizedProfilePath = Path.GetFullPath(normalizedProfilePath);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return TestRunProfileLoadResult.Failure(ExecutionError.InvalidArgument(
                $"profilePath is invalid: {profilePath}."));
        }

        if (!File.Exists(normalizedProfilePath))
        {
            return TestRunProfileLoadResult.Failure(ExecutionError.InvalidArgument(
                $"profilePath does not exist: {normalizedProfilePath}"));
        }

        string json;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            json = await File.ReadAllTextAsync(normalizedProfilePath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            return TestRunProfileLoadResult.Failure(ExecutionError.InternalError(
                $"Failed to read profile file: {normalizedProfilePath}. {exception.Message}"));
        }

        JsonElement root;
        try
        {
            using var document = JsonDocument.Parse(json);
            root = document.RootElement.Clone();
        }
        catch (JsonException exception)
        {
            return TestRunProfileLoadResult.Failure(ExecutionError.InvalidArgument(
                $"profile parse error: {exception.Message}"));
        }

        if (!TryParseProfile(root, out var profile, out var errorMessage))
        {
            return TestRunProfileLoadResult.Failure(ExecutionError.InvalidArgument(errorMessage!));
        }

        return TestRunProfileLoadResult.Success(profile!);
    }

    /// <summary> Parses one profile JSON root by strict contract. </summary>
    /// <param name="root"> The profile JSON root element. </param>
    /// <param name="profile"> The parsed profile when successful. </param>
    /// <param name="errorMessage"> The parse error when parsing fails. </param>
    /// <returns> <see langword="true" /> when parsing succeeds; otherwise <see langword="false" />. </returns>
    private static bool TryParseProfile (
        JsonElement root,
        out TestRunProfile? profile,
        out string? errorMessage)
    {
        profile = null;
        errorMessage = null;

        if (root.ValueKind != JsonValueKind.Object)
        {
            errorMessage = "profile content must be an object.";
            return false;
        }

        foreach (var property in root.EnumerateObject())
        {
            if (!AllowedProperties.Contains(property.Name))
            {
                errorMessage = $"profile contains unknown property: {property.Name}";
                return false;
            }
        }

        if (!TryReadRequiredInt32(root, "schemaVersion", out var schemaVersion, out errorMessage))
        {
            return false;
        }

        if (schemaVersion != TestRunProfile.SchemaVersionValue)
        {
            errorMessage = $"schemaVersion must be {TestRunProfile.SchemaVersionValue}. Actual: {schemaVersion}";
            return false;
        }

        if (!TryReadRequiredString(root, "projectPath", out var projectPath, out errorMessage))
        {
            return false;
        }

        if (!TryReadRequiredNullableString(root, "unityVersion", out var unityVersion, out errorMessage))
        {
            return false;
        }

        if (!TryReadRequiredNullableString(root, "unityEditorPath", out var unityEditorPath, out errorMessage))
        {
            return false;
        }

        if (!TryReadRequiredString(root, "testPlatform", out var testPlatform, out errorMessage))
        {
            return false;
        }

        if (!TryReadRequiredNullableString(root, "buildTarget", out var buildTarget, out errorMessage))
        {
            return false;
        }

        if (!TryReadRequiredNullableString(root, "testFilter", out var testFilter, out errorMessage))
        {
            return false;
        }

        if (!TryReadRequiredStringArray(root, "testCategories", out var testCategories, out errorMessage))
        {
            return false;
        }

        if (!TryReadRequiredStringArray(root, "assemblyNames", out var assemblyNames, out errorMessage))
        {
            return false;
        }

        if (!TryReadRequiredNullableString(root, "testSettingsPath", out var testSettingsPath, out errorMessage))
        {
            return false;
        }

        if (!TryReadRequiredPositiveInt32(root, "timeout", out var timeoutMilliseconds, out errorMessage))
        {
            return false;
        }

        profile = new TestRunProfile
        {
            SchemaVersion = schemaVersion,
            ProjectPath = projectPath,
            UnityVersion = unityVersion,
            UnityEditorPath = unityEditorPath,
            TestPlatform = testPlatform,
            BuildTarget = buildTarget,
            TestFilter = testFilter,
            TestCategories = testCategories,
            AssemblyNames = assemblyNames,
            TestSettingsPath = testSettingsPath,
            Timeout = timeoutMilliseconds,
        };
        return true;
    }

    /// <summary> Reads one required object property. </summary>
    /// <param name="root"> The source object. </param>
    /// <param name="propertyName"> The required property name. </param>
    /// <param name="property"> The property value when found. </param>
    /// <param name="errorMessage"> The error message when missing. </param>
    /// <returns> <see langword="true" /> when property exists; otherwise <see langword="false" />. </returns>
    private static bool TryReadRequiredProperty (
        JsonElement root,
        string propertyName,
        out JsonElement property,
        out string? errorMessage)
    {
        if (!root.TryGetProperty(propertyName, out property))
        {
            errorMessage = $"profile is missing required property: {propertyName}";
            return false;
        }

        errorMessage = null;
        return true;
    }

    /// <summary> Reads one required int32 property. </summary>
    /// <param name="root"> The source object. </param>
    /// <param name="propertyName"> The required property name. </param>
    /// <param name="value"> The parsed value when successful. </param>
    /// <param name="errorMessage"> The error message when parsing fails. </param>
    /// <returns> <see langword="true" /> when parsing succeeds; otherwise <see langword="false" />. </returns>
    private static bool TryReadRequiredInt32 (
        JsonElement root,
        string propertyName,
        out int value,
        out string? errorMessage)
    {
        value = default;
        if (!TryReadRequiredProperty(root, propertyName, out var property, out errorMessage))
        {
            return false;
        }

        if (property.ValueKind != JsonValueKind.Number || !property.TryGetInt32(out value))
        {
            errorMessage = $"profile property '{propertyName}' must be int32.";
            return false;
        }

        errorMessage = null;
        return true;
    }

    /// <summary> Reads one required positive int32 property. </summary>
    /// <param name="root"> The source object. </param>
    /// <param name="propertyName"> The required property name. </param>
    /// <param name="value"> The parsed value when successful. </param>
    /// <param name="errorMessage"> The error message when parsing fails. </param>
    /// <returns> <see langword="true" /> when parsing succeeds; otherwise <see langword="false" />. </returns>
    private static bool TryReadRequiredPositiveInt32 (
        JsonElement root,
        string propertyName,
        out int value,
        out string? errorMessage)
    {
        if (!TryReadRequiredInt32(root, propertyName, out value, out errorMessage))
        {
            return false;
        }

        if (value <= 0)
        {
            errorMessage = $"profile property '{propertyName}' must be in range 1..{int.MaxValue}. Actual: {value}";
            return false;
        }

        errorMessage = null;
        return true;
    }

    /// <summary> Reads one required string property. </summary>
    /// <param name="root"> The source object. </param>
    /// <param name="propertyName"> The required property name. </param>
    /// <param name="value"> The parsed value when successful. </param>
    /// <param name="errorMessage"> The error message when parsing fails. </param>
    /// <returns> <see langword="true" /> when parsing succeeds; otherwise <see langword="false" />. </returns>
    private static bool TryReadRequiredString (
        JsonElement root,
        string propertyName,
        out string value,
        out string? errorMessage)
    {
        value = string.Empty;
        if (!TryReadRequiredProperty(root, propertyName, out var property, out errorMessage))
        {
            return false;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            errorMessage = $"profile property '{propertyName}' must be string.";
            return false;
        }

        value = property.GetString() ?? string.Empty;
        errorMessage = null;
        return true;
    }

    /// <summary> Reads one required nullable string property. </summary>
    /// <param name="root"> The source object. </param>
    /// <param name="propertyName"> The required property name. </param>
    /// <param name="value"> The parsed value when successful. </param>
    /// <param name="errorMessage"> The error message when parsing fails. </param>
    /// <returns> <see langword="true" /> when parsing succeeds; otherwise <see langword="false" />. </returns>
    private static bool TryReadRequiredNullableString (
        JsonElement root,
        string propertyName,
        out string? value,
        out string? errorMessage)
    {
        value = null;
        if (!TryReadRequiredProperty(root, propertyName, out var property, out errorMessage))
        {
            return false;
        }

        if (property.ValueKind == JsonValueKind.Null)
        {
            errorMessage = null;
            return true;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            errorMessage = $"profile property '{propertyName}' must be string or null.";
            return false;
        }

        value = property.GetString();
        errorMessage = null;
        return true;
    }

    /// <summary> Reads one required string-array property. </summary>
    /// <param name="root"> The source object. </param>
    /// <param name="propertyName"> The required property name. </param>
    /// <param name="value"> The parsed value when successful. </param>
    /// <param name="errorMessage"> The error message when parsing fails. </param>
    /// <returns> <see langword="true" /> when parsing succeeds; otherwise <see langword="false" />. </returns>
    private static bool TryReadRequiredStringArray (
        JsonElement root,
        string propertyName,
        out string[] value,
        out string? errorMessage)
    {
        value = Array.Empty<string>();
        if (!TryReadRequiredProperty(root, propertyName, out var property, out errorMessage))
        {
            return false;
        }

        if (property.ValueKind != JsonValueKind.Array)
        {
            errorMessage = $"profile property '{propertyName}' must be string array.";
            return false;
        }

        var parsedValues = new List<string>();
        foreach (var element in property.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.String)
            {
                errorMessage = $"profile property '{propertyName}' must be string array.";
                return false;
            }

            parsedValues.Add(element.GetString() ?? string.Empty);
        }

        value = parsedValues.ToArray();
        errorMessage = null;
        return true;
    }
}