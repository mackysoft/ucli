using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;
using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.UnityIntegration.Indexing.Core;

namespace MackySoft.Ucli.Features.OperationCatalog.Catalog.Source;

/// <summary> Reads source ops catalog data and persists refreshed read-index artifacts on a best-effort basis. </summary>
internal sealed class OpsCatalogSourceRefreshService : IOpsCatalogSourceRefreshService
{
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
        OpsPreflightContext context,
        string fallbackReason,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(fallbackReason);

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
            return OpsCatalogSourceRefreshResult.Failure(
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
        var combinedFallbackReason = ReadIndexAccessUtilities.CombineFallbackReasons(fallbackReason, persistFailure);
        return OpsCatalogSourceRefreshResult.Success(operations, response.GeneratedAtUtc, combinedFallbackReason);
    }

    private async ValueTask<string?> TryPersistCatalog (
        OpsPreflightContext context,
        DateTimeOffset generatedAtUtc,
        IReadOnlyList<IndexOpEntryJsonContract> operations,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var persistenceInput = await TryCreatePersistenceInput(context, cancellationToken).ConfigureAwait(false);
        if (persistenceInput == null)
        {
            return "Failed to persist refreshed ops readIndex because input fingerprint could not be computed.";
        }

        try
        {
            await artifactWriter.WriteOpsCatalog(
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

        var coreSnapshot = await inputFingerprintProvider.TryComputeCore(
                context.Context.UnityProject.UnityProjectRoot,
                cancellationToken)
            .ConfigureAwait(false);
        if (coreSnapshot == null)
        {
            return null;
        }

        var persistedArtifacts = await persistedArtifactsReader.Read(
                context.Context.UnityProject,
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
                context.Context.UnityProject.UnityProjectRoot,
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
