using System.Text.Json;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Json;

namespace MackySoft.Ucli.Application.Features.Testing.Run.Configuration;

/// <summary> Implements strict JSON profile parsing for test-run configuration input. </summary>
internal sealed class TestRunProfileLoader : ITestRunProfileLoader
{
    private static readonly HashSet<string> AllowedProperties = new(StringComparer.Ordinal)
    {
        "schemaVersion",
        "projectPath",
        "unityVersion",
        "unityEditorPath",
        "testPlatform",
        "testFilter",
        "testCategories",
        "assemblyNames",
        "testSettingsPath",
        "timeout",
    };

    private readonly ITestRunProfileJsonReader profileJsonReader;

    /// <summary> Initializes a new instance of the <see cref="TestRunProfileLoader" /> class. </summary>
    /// <param name="profileJsonReader"> The raw profile JSON reader dependency. </param>
    public TestRunProfileLoader (ITestRunProfileJsonReader profileJsonReader)
    {
        this.profileJsonReader = profileJsonReader ?? throw new ArgumentNullException(nameof(profileJsonReader));
    }

    /// <inheritdoc />
    public async ValueTask<TestRunProfileLoadResult> LoadAsync (
        string profilePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var jsonReadResult = await profileJsonReader.ReadTextAsync(profilePath, cancellationToken).ConfigureAwait(false);
        if (!jsonReadResult.IsSuccess)
        {
            return TestRunProfileLoadResult.Failure(jsonReadResult.Error!);
        }

        JsonElement root;
        try
        {
            using var document = JsonDocument.Parse(jsonReadResult.Json!);
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
            TestFilter = testFilter,
            TestCategories = NormalizeListValues(testCategories),
            AssemblyNames = NormalizeListValues(assemblyNames),
            TestSettingsPath = testSettingsPath,
            Timeout = timeoutMilliseconds,
        };
        return true;
    }

    private static string CreateMissingRequiredPropertyError (string propertyName)
    {
        return $"profile is missing required property: {propertyName}";
    }

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

    private static string[] NormalizeListValues (string[] values)
    {
        return values
            .SelectMany(static value => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(static value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string CreateInt32TypeMismatchError (string propertyName)
    {
        return $"profile property '{propertyName}' must be int32.";
    }

    private static string CreateStringTypeMismatchError (string propertyName)
    {
        return $"profile property '{propertyName}' must be string.";
    }

    private static string CreateNullableStringTypeMismatchError (string propertyName)
    {
        return $"profile property '{propertyName}' must be string or null.";
    }

    private static string CreateStringArrayTypeMismatchError (string propertyName)
    {
        return $"profile property '{propertyName}' must be string array.";
    }
}
