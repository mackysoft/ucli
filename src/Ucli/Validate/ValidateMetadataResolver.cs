using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Index;
using MackySoft.Ucli.Operations;
using MackySoft.Ucli.ReadIndex;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Validate;

/// <summary> Resolves metadata for <c>validate</c> by consulting persisted read-index only. </summary>
internal sealed class ValidateMetadataResolver : IValidateMetadataResolver
{
    private const string ReadIndexDisabledReason = "readIndex disabled by mode.";

    private readonly IPersistedOpsCatalogSnapshotLoader persistedOpsCatalogSnapshotLoader;

    /// <summary> Initializes a new instance of the <see cref="ValidateMetadataResolver" /> class. </summary>
    /// <param name="persistedOpsCatalogSnapshotLoader"> The persisted snapshot loader dependency. </param>
    public ValidateMetadataResolver (
        IPersistedOpsCatalogSnapshotLoader persistedOpsCatalogSnapshotLoader)
    {
        this.persistedOpsCatalogSnapshotLoader = persistedOpsCatalogSnapshotLoader ?? throw new ArgumentNullException(nameof(persistedOpsCatalogSnapshotLoader));
    }

    /// <inheritdoc />
    public async ValueTask<ValidateMetadataResolutionResult> Resolve (
        ResolvedUnityProjectContext unityProject,
        ReadIndexMode readIndexMode,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);

        if (readIndexMode == ReadIndexMode.Disabled)
        {
            return ValidateMetadataResolutionResult.Success(
                RequestStaticValidationCatalog.Unavailable,
                CreateReadIndexMiss(ReadIndexDisabledReason));
        }

        var persistedSnapshotResult = await persistedOpsCatalogSnapshotLoader.Load(
                unityProject,
                cancellationToken)
            .ConfigureAwait(false);
        if (!persistedSnapshotResult.IsSuccess)
        {
            return HandleSnapshotLoadFailure(persistedSnapshotResult.Error!, readIndexMode);
        }

        var persistedSnapshot = persistedSnapshotResult.Snapshot!;
        var freshnessResult = IndexFreshnessPolicy.ApplyModeConstraint(readIndexMode, persistedSnapshot.Freshness);
        if (!freshnessResult.IsSuccess)
        {
            return ValidateMetadataResolutionResult.Failure(
                CreateReadIndexHit(
                    freshnessResult.Freshness,
                    persistedSnapshot.GeneratedAtUtc,
                    freshnessResult.Error!.Message),
                freshnessResult.Error.Code,
                freshnessResult.Error.Message);
        }

        IReadOnlyList<UcliOperationDescriptor> operations;
        try
        {
            operations = OperationDescriptorMapper.Map(persistedSnapshot.Entries, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            var message = $"Index contract file 'ops.catalog.json' is malformed. {exception.Message}";
            return ValidateMetadataResolutionResult.Failure(
                CreateReadIndexMiss(message),
                IpcErrorCodes.ReadIndexFormatInvalid,
                message);
        }

        return ValidateMetadataResolutionResult.Success(
            RequestStaticValidationCatalog.Available(operations),
            CreateReadIndexHit(
                persistedSnapshot.Freshness,
                persistedSnapshot.GeneratedAtUtc,
                fallbackReason: null));
    }

    private static ValidateMetadataResolutionResult HandleSnapshotLoadFailure (
        IndexServiceError error,
        ReadIndexMode readIndexMode)
    {
        ArgumentNullException.ThrowIfNull(error);

        if ((readIndexMode == ReadIndexMode.AllowStale)
            && string.Equals(error.Code, IpcErrorCodes.ReadIndexBootstrapFailed, StringComparison.Ordinal))
        {
            return ValidateMetadataResolutionResult.Success(
                RequestStaticValidationCatalog.Unavailable,
                CreateReadIndexMiss(error.Message));
        }

        return ValidateMetadataResolutionResult.Failure(
            CreateReadIndexMiss(error.Message),
            error.Code,
            error.Message);
    }

    private static ReadIndexInfo CreateReadIndexMiss (string fallbackReason)
    {
        return new ReadIndexInfo(
            Used: false,
            Hit: false,
            Source: ReadIndexInfoTextCodec.SourceIndex,
            Freshness: ReadIndexInfoTextCodec.FreshnessProbable,
            GeneratedAtUtc: null,
            FallbackReason: fallbackReason);
    }

    private static ReadIndexInfo CreateReadIndexHit (
        IndexFreshness freshness,
        DateTimeOffset generatedAtUtc,
        string? fallbackReason)
    {
        return new ReadIndexInfo(
            Used: true,
            Hit: true,
            Source: ReadIndexInfoTextCodec.SourceIndex,
            Freshness: ReadIndexInfoTextCodec.MapFreshness(freshness),
            GeneratedAtUtc: generatedAtUtc,
            FallbackReason: fallbackReason);
    }
}