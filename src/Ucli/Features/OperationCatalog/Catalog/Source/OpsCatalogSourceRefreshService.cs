using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Indexing.Core;

namespace MackySoft.Ucli.Features.OperationCatalog.Catalog.Source;

/// <summary> Reads source ops catalog data and persists refreshed read-index artifacts on a best-effort basis. </summary>
internal sealed class OpsCatalogSourceRefreshService : IOpsCatalogSourceRefreshService
{
    private const int MaxCatalogStabilityAttempts = 2;

    private const string InputFingerprintFailureMessage
        = "Failed to persist refreshed ops readIndex because input fingerprint could not be computed.";

    private const string InputInstabilityFailureMessage
        = "Failed to persist refreshed ops readIndex because project inputs changed while the catalog was being read.";

    private const string RetryCatalogReadFailurePrefix
        = "Failed to persist refreshed ops readIndex because retry catalog read failed.";

    private readonly IOpsCatalogReader opsCatalogReader;

    private readonly IPersistedOpsCatalogPersistenceArtifactsReader persistedArtifactsReader;

    private readonly IReadIndexInputFingerprintProvider inputFingerprintProvider;

    private readonly IReadIndexArtifactWriter artifactWriter;

    /// <summary> Initializes a new instance of the <see cref="OpsCatalogSourceRefreshService" /> class. </summary>
    public OpsCatalogSourceRefreshService (
        IOpsCatalogReader opsCatalogReader,
        IPersistedOpsCatalogPersistenceArtifactsReader persistedArtifactsReader,
        IReadIndexInputFingerprintProvider inputFingerprintProvider,
        IReadIndexArtifactWriter artifactWriter)
    {
        this.opsCatalogReader = opsCatalogReader ?? throw new ArgumentNullException(nameof(opsCatalogReader));
        this.persistedArtifactsReader = persistedArtifactsReader ?? throw new ArgumentNullException(nameof(persistedArtifactsReader));
        this.inputFingerprintProvider = inputFingerprintProvider ?? throw new ArgumentNullException(nameof(inputFingerprintProvider));
        this.artifactWriter = artifactWriter ?? throw new ArgumentNullException(nameof(artifactWriter));
    }

    /// <inheritdoc />
    public async ValueTask<OpsCatalogSourceRefreshResult> Refresh (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UnityExecutionMode mode,
        TimeSpan timeout,
        bool failFast,
        string fallbackReason,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        ArgumentException.ThrowIfNullOrWhiteSpace(fallbackReason);

        IpcOpsReadResponse? response = null;
        string? persistFailure = null;
        for (var attempt = 0; attempt < MaxCatalogStabilityAttempts; attempt++)
        {
            var attemptResult = await TryReadAndPersistCatalog(
                    project,
                    config,
                    mode,
                    timeout,
                    failFast,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!attemptResult.FetchResult.IsSuccess)
            {
                if (response != null)
                {
                    persistFailure = ReadIndexAccessUtilities.CombineFallbackReasons(
                        persistFailure,
                        $"{RetryCatalogReadFailurePrefix} {attemptResult.FetchResult.Message}");
                    break;
                }

                return OpsCatalogSourceRefreshResult.Failure(
                    attemptResult.FetchResult.Message,
                    attemptResult.FetchResult.ErrorCode!);
            }

            response = attemptResult.FetchResult.Response!;
            persistFailure = attemptResult.PersistFailure;
            if (!attemptResult.ShouldRetry)
            {
                break;
            }
        }

        var combinedFallbackReason = ReadIndexAccessUtilities.CombineFallbackReasons(fallbackReason, persistFailure);
        return OpsCatalogSourceRefreshResult.Success(
            response!.Operations!.ToArray(),
            response.GeneratedAtUtc,
            combinedFallbackReason);
    }

    private async ValueTask<(OpsCatalogFetchResult FetchResult, string? PersistFailure, bool ShouldRetry)> TryReadAndPersistCatalog (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UnityExecutionMode mode,
        TimeSpan timeout,
        bool failFast,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var snapshotBeforeRead = await inputFingerprintProvider.TryComputeCore(
                project,
                cancellationToken)
            .ConfigureAwait(false);

        var fetchResult = await opsCatalogReader.Read(
                project,
                config,
                mode,
                timeout,
                failFast,
                requireReadinessGate: true,
                cancellationToken)
            .ConfigureAwait(false);
        if (!fetchResult.IsSuccess)
        {
            return (fetchResult, null, false);
        }

        if (snapshotBeforeRead == null)
        {
            return (fetchResult, InputFingerprintFailureMessage, false);
        }

        var snapshotAfterRead = await inputFingerprintProvider.TryComputeCore(
                project,
                cancellationToken)
            .ConfigureAwait(false);
        if (snapshotAfterRead == null)
        {
            return (fetchResult, InputFingerprintFailureMessage, false);
        }

        // NOTE: Stamp sourceInputsHash only when the same core input snapshot is observed
        // before and after the live Unity read. Otherwise a stale catalog can be marked fresh.
        if (!Equals(snapshotBeforeRead, snapshotAfterRead))
        {
            return (fetchResult, InputInstabilityFailureMessage, true);
        }

        var persistenceInput = await CreatePersistenceInput(project, snapshotAfterRead, cancellationToken).ConfigureAwait(false);

        try
        {
            await artifactWriter.WriteOpsCatalog(
                    project.RepositoryRoot,
                    project.ProjectFingerprint,
                    fetchResult.Response!.GeneratedAtUtc,
                    fetchResult.Response.Operations!.ToArray(),
                    persistenceInput.SourceInputsHash,
                    persistenceInput.ManifestInputSnapshot,
                    cancellationToken)
                .ConfigureAwait(false);
            return (fetchResult, null, false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // NOTE:
            // Source results remain authoritative even when local read-index persistence fails.
            // The failure is surfaced in payload.readIndex.fallbackReason instead of failing the command.
            return (fetchResult, $"Failed to persist refreshed ops readIndex. {exception.Message}", false);
        }
    }

    private async ValueTask<OpsCatalogPersistenceInput> CreatePersistenceInput (
        ResolvedUnityProjectContext project,
        ReadIndexCoreInputHashSnapshot coreSnapshot,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var persistedArtifacts = await persistedArtifactsReader.Read(
                project,
                cancellationToken)
            .ConfigureAwait(false);
        if (persistedArtifacts.InputsManifest != null)
        {
            // NOTE:
            // Reuse persisted asset lookup hashes to keep inputs/manifest.json aligned with existing
            // lookup artifacts while avoiding an unnecessary Assets/ full scan on the common ops refresh path.
            var manifest = persistedArtifacts.InputsManifest;
            return new OpsCatalogPersistenceInput(
                SourceInputsHash: coreSnapshot.CombinedHash,
                ManifestInputSnapshot: new ReadIndexInputHashSnapshot(
                    ScriptAssembliesHash: coreSnapshot.ScriptAssembliesHash,
                    PackagesManifestHash: coreSnapshot.PackagesManifestHash,
                    PackagesLockHash: coreSnapshot.PackagesLockHash,
                    AssemblyDefinitionHash: coreSnapshot.AssemblyDefinitionHash,
                    AssetsContentHash: manifest.AssetsContentHash!,
                    AssetSearchHash: manifest.AssetSearchHash!,
                    GuidPathHash: manifest.GuidPathHash!,
                    CombinedHash: coreSnapshot.CombinedHash));
        }

        if (persistedArtifacts.HasPersistedAssetLookupArtifacts)
        {
            // NOTE:
            // Do not regenerate lookup hashes from the live filesystem when persisted lookup artifacts still exist.
            // Persist ops.catalog.json only; the target artifact carries its own freshness hash.
            return new OpsCatalogPersistenceInput(
                SourceInputsHash: coreSnapshot.CombinedHash,
                ManifestInputSnapshot: null);
        }

        var fullSnapshot = await inputFingerprintProvider.TryCompute(
                project,
                cancellationToken)
            .ConfigureAwait(false);
        return new OpsCatalogPersistenceInput(
            SourceInputsHash: coreSnapshot.CombinedHash,
            ManifestInputSnapshot: fullSnapshot);
    }

    private sealed record OpsCatalogPersistenceInput (
        string SourceInputsHash,
        ReadIndexInputHashSnapshot? ManifestInputSnapshot);
}
