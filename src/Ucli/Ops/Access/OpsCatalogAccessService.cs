using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Index;
using MackySoft.Ucli.Ops.Preflight;
using MackySoft.Ucli.ReadIndex;

namespace MackySoft.Ucli.Ops.Access;

/// <summary> Implements ops catalog access flow across read-index and source fallback paths. </summary>
internal sealed class OpsCatalogAccessService : IOpsCatalogAccessService
{
    private readonly IPersistedOpsCatalogSnapshotLoader persistedOpsCatalogSnapshotLoader;

    private readonly IIndexCatalogReader indexCatalogReader;

    private readonly IIndexInputFingerprintCalculator indexInputFingerprintCalculator;

    private readonly IOpsCatalogReader opsCatalogReader;

    private readonly IOpsCatalogStore opsCatalogStore;

    /// <summary> Initializes a new instance of the <see cref="OpsCatalogAccessService" /> class. </summary>
    /// <param name="persistedOpsCatalogSnapshotLoader"> The persisted snapshot loader dependency. </param>
    /// <param name="indexCatalogReader"> The persisted index catalog reader dependency. </param>
    /// <param name="indexInputFingerprintCalculator"> The read-index input fingerprint calculator dependency. </param>
    /// <param name="opsCatalogReader"> The ops catalog reader dependency. </param>
    /// <param name="opsCatalogStore"> The ops catalog persistence dependency. </param>
    public OpsCatalogAccessService (
        IPersistedOpsCatalogSnapshotLoader persistedOpsCatalogSnapshotLoader,
        IIndexCatalogReader indexCatalogReader,
        IIndexInputFingerprintCalculator indexInputFingerprintCalculator,
        IOpsCatalogReader opsCatalogReader,
        IOpsCatalogStore opsCatalogStore)
    {
        this.persistedOpsCatalogSnapshotLoader = persistedOpsCatalogSnapshotLoader ?? throw new ArgumentNullException(nameof(persistedOpsCatalogSnapshotLoader));
        this.indexCatalogReader = indexCatalogReader ?? throw new ArgumentNullException(nameof(indexCatalogReader));
        this.indexInputFingerprintCalculator = indexInputFingerprintCalculator ?? throw new ArgumentNullException(nameof(indexInputFingerprintCalculator));
        this.opsCatalogReader = opsCatalogReader ?? throw new ArgumentNullException(nameof(opsCatalogReader));
        this.opsCatalogStore = opsCatalogStore ?? throw new ArgumentNullException(nameof(opsCatalogStore));
    }

    /// <inheritdoc />
    public async ValueTask<OpsCatalogReadResult> Read (
        OpsPreflightContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(context);

        if (context.ReadIndexMode == ReadIndexMode.Disabled)
        {
            return await ReadCatalogFromSource(
                    context,
                    "readIndex disabled by mode.",
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var persistedSnapshotResult = await persistedOpsCatalogSnapshotLoader.Load(
                context.Context.UnityProject,
                cancellationToken)
            .ConfigureAwait(false);
        if (!persistedSnapshotResult.IsSuccess)
        {
            if (string.Equals(persistedSnapshotResult.Error!.Code, IpcErrorCodes.InvalidArgument, StringComparison.Ordinal))
            {
                return OpsCatalogReadResult.Failure(
                    persistedSnapshotResult.Error.Message,
                    IpcErrorCodes.InvalidArgument);
            }

            return await ReadCatalogFromSource(
                    context,
                    persistedSnapshotResult.Error.Message,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var persistedSnapshot = persistedSnapshotResult.Snapshot!;

        if (context.ReadIndexMode == ReadIndexMode.AllowStale || persistedSnapshot.Freshness == IndexFreshness.Fresh)
        {
            return OpsCatalogReadResult.Success(
                new OpsCatalogReadOutput(
                    Operations: persistedSnapshot.Entries.ToArray(),
                    AccessInfo: new OpsCatalogAccessInfo(
                        Used: true,
                        Hit: true,
                        Source: OpsCatalogSource.Index,
                        Freshness: persistedSnapshot.Freshness,
                        GeneratedAtUtc: persistedSnapshot.GeneratedAtUtc,
                        FallbackReason: null)),
                "Read-index ops catalog hit.");
        }

        return await ReadCatalogFromSource(
                context,
                $"Existing ops index freshness is '{DescribeFreshness(persistedSnapshot.Freshness)}'.",
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<OpsCatalogReadResult> ReadCatalogFromSource (
        OpsPreflightContext context,
        string fallbackReason,
        CancellationToken cancellationToken)
    {
        var fetchResult = await opsCatalogReader.Read(
                context.Context.UnityProject,
                context.Context.Config,
                context.Mode,
                context.Timeout,
                context.FailFast,
                requireReadinessGate: true,
                cancellationToken)
            .ConfigureAwait(false);
        if (!fetchResult.IsSuccess)
        {
            return OpsCatalogReadResult.Failure(
                fetchResult.Message,
                fetchResult.ErrorCode!);
        }

        var response = fetchResult.Response!;
        var operations = response.Operations!.ToArray();
        var persistFailure = await TryPersistCatalog(
                context,
                response.GeneratedAtUtc,
                operations,
                cancellationToken)
            .ConfigureAwait(false);

        return OpsCatalogReadResult.Success(
            new OpsCatalogReadOutput(
                Operations: operations,
                AccessInfo: new OpsCatalogAccessInfo(
                    Used: false,
                    Hit: true,
                    Source: OpsCatalogSource.Source,
                    Freshness: IndexFreshness.Fresh,
                    GeneratedAtUtc: response.GeneratedAtUtc,
                    FallbackReason: CombineFallbackReasons(fallbackReason, persistFailure))),
            "Ops catalog read completed.");
    }

    private async ValueTask<string?> TryPersistCatalog (
        OpsPreflightContext context,
        DateTimeOffset generatedAtUtc,
        IReadOnlyList<IndexOpEntryJsonContract> operations,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var persistenceInput = await TryCreatePersistenceInput(
                context,
                cancellationToken)
            .ConfigureAwait(false);
        if (persistenceInput == null)
        {
            return "Failed to persist refreshed ops readIndex because input fingerprint could not be computed.";
        }

        try
        {
            await opsCatalogStore.Write(
                    context.Context.UnityProject.RepositoryRoot,
                    context.Context.UnityProject.ProjectFingerprint,
                    generatedAtUtc,
                    operations,
                    persistenceInput.SourceInputsHash,
                    persistenceInput.ManifestInputSnapshot,
                    cancellationToken)
                .ConfigureAwait(false);
            return null;
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
            return $"Failed to persist refreshed ops readIndex. {exception.Message}";
        }
    }

    private async ValueTask<OpsCatalogPersistenceInput?> TryCreatePersistenceInput (
        OpsPreflightContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var coreSnapshot = await indexInputFingerprintCalculator.TryComputeCore(
                context.Context.UnityProject.UnityProjectRoot,
                cancellationToken)
            .ConfigureAwait(false);
        if (coreSnapshot == null)
        {
            return null;
        }

        var manifestResult = await indexCatalogReader.ReadInputsManifest(
                context.Context.UnityProject.RepositoryRoot,
                context.Context.UnityProject.ProjectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        if (manifestResult.IsSuccess)
        {
            // NOTE:
            // Reuse the persisted asset lookup hashes to keep inputs/manifest.json aligned with existing
            // lookup artifacts while avoiding an unnecessary Assets/ full scan on the common ops refresh path.
            var manifest = manifestResult.Value!;
            return new OpsCatalogPersistenceInput(
                SourceInputsHash: coreSnapshot.CombinedHash,
                ManifestInputSnapshot: new IndexInputHashSnapshot(
                    ScriptAssembliesHash: coreSnapshot.ScriptAssembliesHash,
                    PackagesManifestHash: coreSnapshot.PackagesManifestHash,
                    PackagesLockHash: coreSnapshot.PackagesLockHash,
                    AssemblyDefinitionHash: coreSnapshot.AssemblyDefinitionHash,
                    AssetsContentHash: manifest.AssetsContentHash!,
                    AssetSearchHash: manifest.AssetSearchHash!,
                    GuidPathHash: manifest.GuidPathHash!,
                    CombinedHash: coreSnapshot.CombinedHash));
        }

        if (await HasPersistedAssetLookupArtifacts(context, cancellationToken).ConfigureAwait(false))
        {
            // NOTE:
            // Do not regenerate lookup hashes from the live filesystem when persisted lookup artifacts still exist.
            // Persist ops.catalog.json only; the target artifact carries its own freshness hash.
            return new OpsCatalogPersistenceInput(
                SourceInputsHash: coreSnapshot.CombinedHash,
                ManifestInputSnapshot: null);
        }

        var fullSnapshot = await indexInputFingerprintCalculator.TryCompute(
                context.Context.UnityProject.UnityProjectRoot,
                cancellationToken)
            .ConfigureAwait(false);
        if (fullSnapshot == null)
        {
            return new OpsCatalogPersistenceInput(
                SourceInputsHash: coreSnapshot.CombinedHash,
                ManifestInputSnapshot: null);
        }

        return new OpsCatalogPersistenceInput(
            SourceInputsHash: coreSnapshot.CombinedHash,
            ManifestInputSnapshot: fullSnapshot);
    }

    private async ValueTask<bool> HasPersistedAssetLookupArtifacts (
        OpsPreflightContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var assetSearchLookupResult = await indexCatalogReader.ReadAssetSearchLookup(
                context.Context.UnityProject.RepositoryRoot,
                context.Context.UnityProject.ProjectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        if (assetSearchLookupResult.IsSuccess)
        {
            return true;
        }

        var guidPathLookupResult = await indexCatalogReader.ReadGuidPathLookup(
                context.Context.UnityProject.RepositoryRoot,
                context.Context.UnityProject.ProjectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        return guidPathLookupResult.IsSuccess;
    }

    private sealed record OpsCatalogPersistenceInput (
        string SourceInputsHash,
        IndexInputHashSnapshot? ManifestInputSnapshot);

    private static string? CombineFallbackReasons (
        string? first,
        string? second)
    {
        if (string.IsNullOrWhiteSpace(first))
        {
            return string.IsNullOrWhiteSpace(second) ? null : second;
        }

        if (string.IsNullOrWhiteSpace(second))
        {
            return first;
        }

        return $"{first} {second}";
    }

    private static string DescribeFreshness (IndexFreshness freshness)
    {
        return freshness switch
        {
            IndexFreshness.Fresh => "fresh",
            IndexFreshness.Probable => "probable",
            IndexFreshness.Stale => "stale",
            _ => throw new ArgumentOutOfRangeException(nameof(freshness), freshness, "Unsupported index freshness."),
        };
    }
}