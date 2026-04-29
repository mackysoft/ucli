using System.Globalization;
using System.Security.Cryptography;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Features.Testing.Run.Configuration;
using MackySoft.Ucli.Infrastructure.Paths;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Shared.Context.Project;
using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Features.Testing.Run.Artifacts;

/// <summary> Implements run-scoped artifact path preparation and metadata lifecycle updates. </summary>
internal sealed class TestRunArtifactsService : ITestRunArtifactsService
{
    private const int MaxRunIdGenerationAttempts = 5;

    private const string RunIdTimestampFormat = "yyyyMMdd_HHmmss'Z'";

    private readonly ITestRunMetaStore metaStore;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="TestRunArtifactsService" /> class with default meta-store dependency. </summary>
    public TestRunArtifactsService ()
        : this(new TestRunMetaStore(), timeProvider: null)
    {
    }

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
    public async ValueTask<ArtifactsPreparationResult> Prepare (
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

            var artifactPaths = new ArtifactPaths(artifactsDir);
            var session = new ArtifactsSession(
                RunId: runId,
                Paths: artifactPaths,
                StartedAtUtc: startedAtUtc);

            try
            {
                await metaStore.Write(
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

    /// <summary> Completes one run-scoped artifacts session by updating completion metadata. </summary>
    /// <param name="configuration"> The resolved test-run configuration. </param>
    /// <param name="session"> The prepared artifacts session. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by caller. </param>
    /// <returns> A task that resolves to the completion result. </returns>
    public async ValueTask<ArtifactsCompletionResult> Complete (
        ResolvedTestRunConfiguration configuration,
        ArtifactsSession session,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(session);

        try
        {
            await metaStore.Write(
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

    /// <summary> Creates one run identifier value. </summary>
    /// <param name="utcNow"> The current UTC timestamp. </param>
    /// <returns> The run identifier value. </returns>
    private static string CreateRunId (DateTimeOffset utcNow)
    {
        var suffix = RandomNumberGenerator.GetHexString(8).ToLowerInvariant();
        return $"{utcNow.ToString(RunIdTimestampFormat, CultureInfo.InvariantCulture)}_{suffix}";
    }
}
