using System.ComponentModel;
using System.Globalization;
using System.Security.Cryptography;
using MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;
using MackySoft.Ucli.Application.Features.Testing.Run.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Infrastructure.Execution;
using MackySoft.Ucli.Infrastructure.Paths;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Features.Testing.Run.Artifacts;

/// <summary> Implements run-scoped artifact path preparation and metadata lifecycle updates. </summary>
internal sealed class TestRunArtifactsService : ITestRunArtifactsService
{
    private const int MaxRunIdGenerationAttempts = 5;

    private const string RunIdTimestampFormat = "yyyyMMdd_HHmmss'Z'";

    private readonly ITestRunMetaStore metaStore;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="TestRunArtifactsService" /> class. </summary>
    /// <param name="metaStore"> The metadata store dependency. </param>
    /// <param name="timeProvider"> The time provider used for metadata timestamps. </param>
    public TestRunArtifactsService (
        ITestRunMetaStore metaStore,
        TimeProvider? timeProvider = null)
    {
        this.metaStore = metaStore ?? throw new ArgumentNullException(nameof(metaStore));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary> Prepares one run-scoped artifact directory and writes initial <c>meta.json</c>. </summary>
    /// <param name="configuration"> The resolved test-run configuration. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by caller. </param>
    /// <returns> A task that resolves to the preparation result. </returns>
    public async ValueTask<ArtifactsPreparationResult> PrepareAsync (
        ResolvedTestRunConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(configuration);

        var unityProject = configuration.UnityProject;

        // NOTE:
        // runId may collide when runs are started in the same second.
        // Retry bounded attempts to avoid sharing one artifact directory across runs.
        for (var attempt = 0; attempt < MaxRunIdGenerationAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var startedAtUtc = timeProvider.GetUtcNow();
            var runId = CreateRunId(startedAtUtc);

            string artifactsDir;
            try
            {
                artifactsDir = UcliStoragePathResolver.ResolveTestRunArtifactsDirectory(
                    unityProject.RepositoryRoot,
                    unityProject.ProjectFingerprint,
                    runId);
            }
            catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
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
                FileSystemAccessBoundary.EnsureSecureDirectory(artifactsDir);
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
            {
                return ArtifactsPreparationResult.Failure(ExecutionError.InternalError(
                    $"Failed to create artifacts directory: {artifactsDir}. {exception.Message}"));
            }

            var artifactPaths = CreateArtifactPaths(artifactsDir);
            var session = new ArtifactsSession(
                RunId: runId,
                Paths: artifactPaths,
                StartedAtUtc: startedAtUtc);

            try
            {
                await metaStore.WriteAsync(
                    configuration,
                    session,
                    finishedAtUtc: startedAtUtc,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
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

    private static ArtifactPaths CreateArtifactPaths (string artifactsDir)
    {
        return new ArtifactPaths(
            ArtifactsDir: artifactsDir,
            MetaJsonPath: Path.Combine(artifactsDir, "meta.json"),
            ResultsXmlPath: Path.Combine(artifactsDir, "results.xml"),
            EditorLogPath: Path.Combine(artifactsDir, "editor.log"),
            ResultsJsonPath: Path.Combine(artifactsDir, "results.json"),
            SummaryJsonPath: Path.Combine(artifactsDir, "summary.json"));
    }

    /// <summary> Completes one run-scoped artifact session by attempting to remove interrupted oneshot editor-log exports and updating completion metadata. </summary>
    /// <param name="configuration"> The resolved test-run configuration. </param>
    /// <param name="session"> The prepared artifacts session. </param>
    /// <param name="target"> The execution target held fixed for this test run; <see cref="UnityExecutionTarget.Oneshot" /> enables interrupted editor-log export cleanup. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by caller. </param>
    /// <returns> A task that resolves to the completion result. </returns>
    public async ValueTask<ArtifactsCompletionResult> CompleteAsync (
        ResolvedTestRunConfiguration configuration,
        ArtifactsSession session,
        UnityExecutionTarget target,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(session);

        try
        {
            if (target == UnityExecutionTarget.Oneshot)
            {
                TryDeleteInterruptedEditorLogExports(session.Paths);
            }

            await metaStore.WriteAsync(
                configuration,
                session,
                finishedAtUtc: timeProvider.GetUtcNow(),
                cancellationToken).ConfigureAwait(false);
            return ArtifactsCompletionResult.Success();
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
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

    /// <summary> Deletes run-scoped editor-log exporter temporary files on a best-effort basis. </summary>
    /// <param name="artifactPaths"> The run-scoped artifact paths that bound the deletion. </param>
    private static void TryDeleteInterruptedEditorLogExports (ArtifactPaths artifactPaths)
    {
        var temporaryFileNamePrefix = Path.GetFileName(artifactPaths.EditorLogPath) + ".tmp.";
        // Best-effort cleanup must not replace the primary test-run result.
        try
        {
            foreach (var path in Directory.EnumerateFiles(
                artifactPaths.ArtifactsDir,
                temporaryFileNamePrefix + "*",
                SearchOption.TopDirectoryOnly))
            {
                if (!ProcessOwnedTemporaryFilePath.TryGetOwnerProcessId(
                    artifactPaths.EditorLogPath,
                    path,
                    out var processId)
                    || !IsOwnerProcessKnownToHaveExited(processId))
                {
                    continue;
                }

                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary> Gets whether the owner is known to have exited, preserving the file when process state cannot be queried. </summary>
    /// <param name="processId"> The owner process identifier encoded in the temporary file path. </param>
    /// <returns> <see langword="true" /> only when the owner process is confirmed not to be alive. </returns>
    private static bool IsOwnerProcessKnownToHaveExited (int processId)
    {
        try
        {
            return !ProcessLivenessProbe.IsAlive(processId);
        }
        catch (Exception exception) when (exception is Win32Exception or NotSupportedException)
        {
            return false;
        }
    }

    /// <summary> Creates one run identifier value. </summary>
    /// <param name="utcNow"> The current UTC timestamp. </param>
    /// <returns> The run identifier value. </returns>
    private static string CreateRunId (DateTimeOffset utcNow)
    {
        var suffix = RandomNumberGenerator.GetHexString(8).ToLowerInvariant();
        return $"{utcNow.ToString(RunIdTimestampFormat, CultureInfo.InvariantCulture)}_{suffix}";
    }
}
