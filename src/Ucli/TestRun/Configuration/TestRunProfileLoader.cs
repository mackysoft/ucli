using System.Text.Json;
using MackySoft.Ucli.Contracts.Paths;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.TestRun.Configuration;

/// <summary> Implements JSON profile loading for test-run configuration input. </summary>
internal sealed class TestRunProfileLoader : ITestRunProfileLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary> Loads one profile JSON file from disk. </summary>
    /// <param name="profilePath"> The profile path value. </param>
    /// <returns> The profile load result. </returns>
    public TestRunProfileLoadResult Load (string profilePath)
    {
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
            json = File.ReadAllText(normalizedProfilePath);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            return TestRunProfileLoadResult.Failure(ExecutionError.InternalError(
                $"Failed to read profile file: {normalizedProfilePath}. {exception.Message}"));
        }

        TestRunProfile? profile;
        try
        {
            profile = JsonSerializer.Deserialize<TestRunProfile>(json, SerializerOptions);
        }
        catch (JsonException exception)
        {
            return TestRunProfileLoadResult.Failure(ExecutionError.InvalidArgument(
                $"profile parse error: {exception.Message}"));
        }

        if (profile is null)
        {
            return TestRunProfileLoadResult.Failure(ExecutionError.InvalidArgument(
                "profile content is empty or invalid."));
        }

        if (profile.SchemaVersion != TestRunProfile.SchemaVersionValue)
        {
            return TestRunProfileLoadResult.Failure(ExecutionError.InvalidArgument(
                $"schemaVersion must be {TestRunProfile.SchemaVersionValue}. Actual: {profile.SchemaVersion}"));
        }

        return TestRunProfileLoadResult.Success(profile);
    }
}