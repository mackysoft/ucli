using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.TestRun.Configuration;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.TestRun.Artifacts;

/// <summary> Implements run-scoped artifact path preparation and metadata lifecycle updates. </summary>
internal sealed class TestRunArtifactsService : ITestRunArtifactsService
{
    private const int MetaSchemaVersion = 1;

    private const int MaxRunIdGenerationAttempts = 5;

    private const string RunIdTimestampFormat = "yyyyMMdd_HHmmss'Z'";

    private const string MetaJsonFileName = "meta.json";

    private const string ResultsXmlFileName = "results.xml";

    private const string EditorLogFileName = "editor.log";

    private const string ResultsJsonFileName = "results.json";

    private const string SummaryJsonFileName = "summary.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    /// <summary> Prepares one run-scoped artifact directory and writes initial <c>meta.json</c>. </summary>
    /// <param name="configuration"> The resolved test-run configuration. </param>
    /// <returns> The preparation result. </returns>
    public ArtifactsPreparationResult Prepare (ResolvedTestRunConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var unityProject = configuration.UnityProject;

        // NOTE:
        // runId may collide when runs are started in the same second.
        // Retry bounded attempts to avoid sharing one artifact directory across runs.
        for (var attempt = 0; attempt < MaxRunIdGenerationAttempts; attempt++)
        {
            var startedAtUtc = DateTimeOffset.UtcNow;
            var runId = CreateRunId(startedAtUtc);

            string artifactsDir;
            try
            {
                artifactsDir = UcliStoragePathResolver.ResolveTestRunArtifactsDirectory(
                    unityProject.RepositoryRoot,
                    unityProject.ProjectFingerprint,
                    runId);
            }
            catch (Exception exception) when (PathFormatExceptionHelper.IsPathFormatException(exception))
            {
                return ArtifactsPreparationResult.Failure(ExecutionError.InvalidArgument(
                    $"Artifacts path is invalid. {exception.Message}"));
            }

            if (Directory.Exists(artifactsDir))
            {
                continue;
            }

            try
            {
                Directory.CreateDirectory(artifactsDir);
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
            {
                return ArtifactsPreparationResult.Failure(ExecutionError.InternalError(
                    $"Failed to create artifacts directory: {artifactsDir}. {exception.Message}"));
            }

            var artifactPaths = CreateArtifactPaths(artifactsDir);
            var session = new ArtifactsSession(
                RunId: runId,
                ArtifactsDir: artifactsDir,
                Paths: artifactPaths,
                StartedAtUtc: startedAtUtc);

            try
            {
                WriteMetaJson(configuration, session, finishedAtUtc: startedAtUtc);
            }
            catch (Exception exception) when (PathFormatExceptionHelper.IsPathFormatException(exception))
            {
                return ArtifactsPreparationResult.Failure(ExecutionError.InvalidArgument(
                    $"Failed to write meta.json due to invalid path: {session.Paths.MetaJsonPath}. {exception.Message}"));
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
            {
                return ArtifactsPreparationResult.Failure(ExecutionError.InternalError(
                    $"Failed to write meta.json: {session.Paths.MetaJsonPath}. {exception.Message}"));
            }

            return ArtifactsPreparationResult.Success(session);
        }

        return ArtifactsPreparationResult.Failure(ExecutionError.InternalError(
            $"Failed to create unique artifacts directory after {MaxRunIdGenerationAttempts} attempts."));
    }

    /// <summary> Completes one run-scoped artifacts session by updating completion metadata. </summary>
    /// <param name="configuration"> The resolved test-run configuration. </param>
    /// <param name="session"> The prepared artifacts session. </param>
    /// <returns> The completion result. </returns>
    public ArtifactsCompletionResult Complete (
        ResolvedTestRunConfiguration configuration,
        ArtifactsSession session)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(session);

        try
        {
            WriteMetaJson(configuration, session, finishedAtUtc: DateTimeOffset.UtcNow);
            return ArtifactsCompletionResult.Success();
        }
        catch (Exception exception) when (PathFormatExceptionHelper.IsPathFormatException(exception))
        {
            return ArtifactsCompletionResult.Failure(ExecutionError.InvalidArgument(
                $"Failed to update meta.json due to invalid path: {session.Paths.MetaJsonPath}. {exception.Message}"));
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            return ArtifactsCompletionResult.Failure(ExecutionError.InternalError(
                $"Failed to update meta.json: {session.Paths.MetaJsonPath}. {exception.Message}"));
        }
    }

    /// <summary> Creates fixed artifact paths under one artifacts directory. </summary>
    /// <param name="artifactsDirectoryPath"> The run artifacts directory path. </param>
    /// <returns> The fixed artifact paths. </returns>
    private static ArtifactPaths CreateArtifactPaths (string artifactsDirectoryPath)
    {
        return new ArtifactPaths(
            MetaJsonPath: Path.Combine(artifactsDirectoryPath, MetaJsonFileName),
            ResultsXmlPath: Path.Combine(artifactsDirectoryPath, ResultsXmlFileName),
            EditorLogPath: Path.Combine(artifactsDirectoryPath, EditorLogFileName),
            ResultsJsonPath: Path.Combine(artifactsDirectoryPath, ResultsJsonFileName),
            SummaryJsonPath: Path.Combine(artifactsDirectoryPath, SummaryJsonFileName));
    }

    /// <summary> Creates one run identifier value. </summary>
    /// <param name="utcNow"> The current UTC timestamp. </param>
    /// <returns> The run identifier value. </returns>
    private static string CreateRunId (DateTimeOffset utcNow)
    {
        var suffix = RandomNumberGenerator.GetHexString(8).ToLowerInvariant();
        return $"{utcNow.ToString(RunIdTimestampFormat, CultureInfo.InvariantCulture)}_{suffix}";
    }

    /// <summary> Writes metadata JSON for one artifacts session. </summary>
    /// <param name="configuration"> The resolved test-run configuration. </param>
    /// <param name="session"> The artifacts session. </param>
    /// <param name="finishedAtUtc"> The completion timestamp to persist. </param>
    private static void WriteMetaJson (
        ResolvedTestRunConfiguration configuration,
        ArtifactsSession session,
        DateTimeOffset finishedAtUtc)
    {
        var payload = new MetaJsonPayload(
            SchemaVersion: MetaSchemaVersion,
            RunId: session.RunId,
            StartedAt: session.StartedAtUtc.ToString("O", CultureInfo.InvariantCulture),
            FinishedAt: finishedAtUtc.ToString("O", CultureInfo.InvariantCulture),
            ProjectPath: configuration.UnityProject.UnityProjectRoot,
            UnityVersion: configuration.UnityVersion,
            UnityEditorPath: configuration.UnityEditorPath,
            Mode: configuration.Mode,
            TestPlatform: TestRunPlatformCodec.ToValue(configuration.TestPlatform),
            BuildTarget: configuration.BuildTarget,
            TestFilter: configuration.TestFilter,
            TestCategories: configuration.TestCategories,
            AssemblyNames: configuration.AssemblyNames,
            TestSettingsPath: configuration.TestSettingsPath,
            ArtifactsDir: session.ArtifactsDir);

        var json = JsonSerializer.Serialize(payload, SerializerOptions);
        File.WriteAllText(session.Paths.MetaJsonPath, json);
    }

    /// <summary> Represents metadata payload for one test-run artifacts session. </summary>
    /// <param name="SchemaVersion"> The metadata schema version. </param>
    /// <param name="RunId"> The run identifier. </param>
    /// <param name="StartedAt"> The run start timestamp in ISO-8601 UTC format. </param>
    /// <param name="FinishedAt"> The run completion timestamp in ISO-8601 UTC format. </param>
    /// <param name="ProjectPath"> The Unity project path. </param>
    /// <param name="UnityVersion"> The resolved Unity version. </param>
    /// <param name="UnityEditorPath"> The resolved Unity editor path. </param>
    /// <param name="Mode"> The execution mode option value. </param>
    /// <param name="TestPlatform"> The test-platform value. </param>
    /// <param name="BuildTarget"> The optional build target value. </param>
    /// <param name="TestFilter"> The optional test-filter value. </param>
    /// <param name="TestCategories"> The normalized test-category values. </param>
    /// <param name="AssemblyNames"> The normalized assembly-name values. </param>
    /// <param name="TestSettingsPath"> The optional test-settings path value. </param>
    /// <param name="ArtifactsDir"> The run artifacts directory path. </param>
    private sealed record MetaJsonPayload (
        int SchemaVersion,
        string RunId,
        string StartedAt,
        string FinishedAt,
        string ProjectPath,
        string UnityVersion,
        string UnityEditorPath,
        string Mode,
        string TestPlatform,
        string? BuildTarget,
        string? TestFilter,
        string[] TestCategories,
        string[] AssemblyNames,
        string? TestSettingsPath,
        string ArtifactsDir);
}