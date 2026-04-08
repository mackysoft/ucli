using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Ops.Preflight;
using MackySoft.Ucli.ReadIndex;

namespace MackySoft.Ucli.Ops.Access;

/// <summary> Implements ops catalog access flow across read-index and source fallback paths. </summary>
internal sealed class OpsCatalogAccessService : IOpsCatalogAccessService
{
    private readonly IPersistedOpsCatalogSnapshotLoader persistedOpsCatalogSnapshotLoader;

    private readonly IIndexInputFingerprintCalculator indexInputFingerprintCalculator;

    private readonly IOpsCatalogReader opsCatalogReader;

    private readonly IOpsCatalogStore opsCatalogStore;

    /// <summary> Initializes a new instance of the <see cref="OpsCatalogAccessService" /> class. </summary>
    /// <param name="persistedOpsCatalogSnapshotLoader"> The persisted snapshot loader dependency. </param>
    /// <param name="indexInputFingerprintCalculator"> The read-index input fingerprint calculator dependency. </param>
    /// <param name="opsCatalogReader"> The ops catalog reader dependency. </param>
    /// <param name="opsCatalogStore"> The ops catalog persistence dependency. </param>
    public OpsCatalogAccessService (
        IPersistedOpsCatalogSnapshotLoader persistedOpsCatalogSnapshotLoader,
        IIndexInputFingerprintCalculator indexInputFingerprintCalculator,
        IOpsCatalogReader opsCatalogReader,
        IOpsCatalogStore opsCatalogStore)
    {
        this.persistedOpsCatalogSnapshotLoader = persistedOpsCatalogSnapshotLoader ?? throw new ArgumentNullException(nameof(persistedOpsCatalogSnapshotLoader));
        this.indexInputFingerprintCalculator = indexInputFingerprintCalculator ?? throw new ArgumentNullException(nameof(indexInputFingerprintCalculator));
        this.opsCatalogReader = opsCatalogReader ?? throw new ArgumentNullException(nameof(opsCatalogReader));
        this.opsCatalogStore = opsCatalogStore ?? throw new ArgumentNullException(nameof(opsCatalogStore));
    }

    /// <inheritdoc />
    public async ValueTask<OpsCatalogReadResult> Read (
        OpsPreflightContext context,
        OpsCommandInput input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(input);

        if (context.ReadIndexMode == ReadIndexMode.Disabled)
        {
            return await ReadCatalogFromSource(
                    context,
                    input,
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
                    input,
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
                input,
                $"Existing ops index freshness is '{DescribeFreshness(persistedSnapshot.Freshness)}'.",
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<OpsCatalogReadResult> ReadCatalogFromSource (
        OpsPreflightContext context,
        OpsCommandInput input,
        string fallbackReason,
        CancellationToken cancellationToken)
    {
        var fetchResult = await opsCatalogReader.Read(
                context.Context.UnityProject,
                context.Context.Config,
                input.Mode,
                input.Timeout,
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

        var inputSnapshot = await indexInputFingerprintCalculator.TryCompute(
                context.Context.UnityProject.UnityProjectRoot,
                cancellationToken)
            .ConfigureAwait(false);
        if (inputSnapshot == null)
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
                    inputSnapshot,
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