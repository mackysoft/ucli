using System.ComponentModel;
using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;
using MackySoft.Ucli.Application.Features.Testing.Run.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Application.Shared.Identifiers;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Execution;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Features.Testing.Run.Artifacts;

/// <summary> Implements run-scoped artifact path preparation and metadata lifecycle updates. </summary>
internal sealed class TestRunArtifactsService : ITestRunArtifactsService
{
    private readonly ITestRunMetaStore metaStore;

    private readonly IGuidGenerator runIdGenerator;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="TestRunArtifactsService" /> class. </summary>
    /// <param name="metaStore"> The metadata store dependency. </param>
    /// <param name="runIdGenerator"> The run identifier generator. </param>
    /// <param name="timeProvider"> The time provider used for metadata timestamps. </param>
    public TestRunArtifactsService (
        ITestRunMetaStore metaStore,
        IGuidGenerator runIdGenerator,
        TimeProvider timeProvider)
    {
        this.metaStore = metaStore ?? throw new ArgumentNullException(nameof(metaStore));
        this.runIdGenerator = runIdGenerator ?? throw new ArgumentNullException(nameof(runIdGenerator));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
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
        var startedAtUtc = timeProvider.GetUtcNow();
        var runId = runIdGenerator.Generate();

        var artifactsDir = UcliStoragePathResolver.ResolveTestRunArtifactsDirectory(
            unityProject.RepositoryRoot,
            unityProject.ProjectFingerprint,
            runId);

        if (File.Exists(artifactsDir.Value) || Directory.Exists(artifactsDir.Value))
        {
            return ArtifactsPreparationResult.Failure(ExecutionError.InternalError(
                $"Test-run artifact directory already exists: {artifactsDir}."));
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
            runId: runId,
            paths: artifactPaths,
            startedAtUtc: startedAtUtc);

        try
        {
            await metaStore.WriteAsync(
                configuration,
                session,
                finishedAtUtc: startedAtUtc,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            return ArtifactsPreparationResult.Failure(ExecutionError.InternalError(
                $"Failed to write meta.json: {session.Paths.MetaJsonPath}. {exception.Message}"));
        }

        return ArtifactsPreparationResult.Success(session);
    }

    private static ArtifactPaths CreateArtifactPaths (AbsolutePath artifactsDir)
    {
        return new ArtifactPaths(
            ArtifactsDir: artifactsDir,
            MetaJsonPath: ResolveArtifactPath(artifactsDir, "meta.json"),
            ResultsXmlPath: ResolveArtifactPath(
                artifactsDir,
                UcliStoragePathNames.TestResultsXmlFileName),
            EditorLogPath: ResolveArtifactPath(
                artifactsDir,
                UcliStoragePathNames.TestEditorLogFileName),
            ResultsJsonPath: ResolveArtifactPath(artifactsDir, "results.json"),
            SummaryJsonPath: ResolveArtifactPath(artifactsDir, "summary.json"));
    }

    private static AbsolutePath ResolveArtifactPath (
        AbsolutePath artifactsDirectory,
        string fileName)
    {
        return ContainedPath.Create(
            artifactsDirectory,
            RootRelativePath.Parse(fileName)).Target;
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
        // Best-effort cleanup must not replace the primary test-run result.
        try
        {
            foreach (var path in Directory.EnumerateFiles(
                artifactPaths.ArtifactsDir.Value,
                EditorLogTemporaryFilePath.FileNameSearchPattern,
                SearchOption.TopDirectoryOnly))
            {
                if (!EditorLogTemporaryFilePath.TryGetOwnerProcessId(
                    Path.GetFileName(path),
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

}
