using System.Text.Json;
using MackySoft.Ucli.Contracts.Json;
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

        var unknownProperty = JsonObjectPropertyReader.FindUnknownProperty(root, AllowedProperties);
        if (!string.IsNullOrEmpty(unknownProperty))
        {
            errorMessage = $"profile contains unknown property: {unknownProperty}";
            return false;
        }

        if (!JsonObjectPropertyReader.TryReadRequiredInt32(
            root,
            "schemaVersion",
            CreateMissingRequiredPropertyError,
            CreateInt32TypeMismatchError,
            noError: null,
            out var schemaVersion,
            out errorMessage))
        {
            return false;
        }

        if (schemaVersion != TestRunProfile.SchemaVersionValue)
        {
            errorMessage = $"schemaVersion must be {TestRunProfile.SchemaVersionValue}. Actual: {schemaVersion}";
            return false;
        }

        if (!JsonObjectPropertyReader.TryReadRequiredString(
            root,
            "projectPath",
            CreateMissingRequiredPropertyError,
            CreateStringTypeMismatchError,
            noError: null,
            out var projectPath,
            out errorMessage))
        {
            return false;
        }

        if (!JsonObjectPropertyReader.TryReadRequiredNullableString(
            root,
            "unityVersion",
            CreateMissingRequiredPropertyError,
            CreateNullableStringTypeMismatchError,
            noError: null,
            out var unityVersion,
            out errorMessage))
        {
            return false;
        }

        if (!JsonObjectPropertyReader.TryReadRequiredNullableString(
            root,
            "unityEditorPath",
            CreateMissingRequiredPropertyError,
            CreateNullableStringTypeMismatchError,
            noError: null,
            out var unityEditorPath,
            out errorMessage))
        {
            return false;
        }

        if (!JsonObjectPropertyReader.TryReadRequiredString(
            root,
            "testPlatform",
            CreateMissingRequiredPropertyError,
            CreateStringTypeMismatchError,
            noError: null,
            out var testPlatform,
            out errorMessage))
        {
            return false;
        }

        if (!JsonObjectPropertyReader.TryReadRequiredNullableString(
            root,
            "buildTarget",
            CreateMissingRequiredPropertyError,
            CreateNullableStringTypeMismatchError,
            noError: null,
            out var buildTarget,
            out errorMessage))
        {
            return false;
        }

        if (!JsonObjectPropertyReader.TryReadRequiredNullableString(
            root,
            "testFilter",
            CreateMissingRequiredPropertyError,
            CreateNullableStringTypeMismatchError,
            noError: null,
            out var testFilter,
            out errorMessage))
        {
            return false;
        }

        if (!JsonObjectPropertyReader.TryReadRequiredStringArray(
            root,
            "testCategories",
            CreateMissingRequiredPropertyError,
            CreateStringArrayTypeMismatchError,
            CreateStringArrayTypeMismatchError,
            noError: null,
            out var testCategories,
            out errorMessage))
        {
            return false;
        }

        if (!JsonObjectPropertyReader.TryReadRequiredStringArray(
            root,
            "assemblyNames",
            CreateMissingRequiredPropertyError,
            CreateStringArrayTypeMismatchError,
            CreateStringArrayTypeMismatchError,
            noError: null,
            out var assemblyNames,
            out errorMessage))
        {
            return false;
        }

        if (!JsonObjectPropertyReader.TryReadRequiredNullableString(
            root,
            "testSettingsPath",
            CreateMissingRequiredPropertyError,
            CreateNullableStringTypeMismatchError,
            noError: null,
            out var testSettingsPath,
            out errorMessage))
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

    /// <summary> Creates error text for missing required profile property. </summary>
    /// <param name="propertyName"> The missing property name. </param>
    /// <returns> The missing-property error text. </returns>
    private static string CreateMissingRequiredPropertyError (string propertyName)
    {
        return $"profile is missing required property: {propertyName}";
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
        if (!JsonObjectPropertyReader.TryReadRequiredInt32(
            root,
            propertyName,
            CreateMissingRequiredPropertyError,
            CreateInt32TypeMismatchError,
            noError: null,
            out value,
            out errorMessage))
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

    /// <summary> Creates error text for int32 type mismatch. </summary>
    /// <param name="propertyName"> The property name. </param>
    /// <returns> The type-mismatch error text. </returns>
    private static string CreateInt32TypeMismatchError (string propertyName)
    {
        return $"profile property '{propertyName}' must be int32.";
    }

    /// <summary> Creates error text for string type mismatch. </summary>
    /// <param name="propertyName"> The property name. </param>
    /// <returns> The type-mismatch error text. </returns>
    private static string CreateStringTypeMismatchError (string propertyName)
    {
        return $"profile property '{propertyName}' must be string.";
    }

    /// <summary> Creates error text for nullable-string type mismatch. </summary>
    /// <param name="propertyName"> The property name. </param>
    /// <returns> The type-mismatch error text. </returns>
    private static string CreateNullableStringTypeMismatchError (string propertyName)
    {
        return $"profile property '{propertyName}' must be string or null.";
    }

    /// <summary> Creates error text for string-array type mismatch. </summary>
    /// <param name="propertyName"> The property name. </param>
    /// <returns> The type-mismatch error text. </returns>
    private static string CreateStringArrayTypeMismatchError (string propertyName)
    {
        return $"profile property '{propertyName}' must be string array.";
    }
}