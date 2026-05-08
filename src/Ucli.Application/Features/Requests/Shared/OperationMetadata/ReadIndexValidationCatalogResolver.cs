using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;
using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;

/// <summary> Resolves static-validation catalogs by consulting persisted read-index artifacts only. </summary>
internal sealed class ReadIndexValidationCatalogResolver : IReadIndexValidationCatalogResolver
{
    private const string ReadIndexDisabledReason = "readIndex disabled by mode.";

    private readonly IPersistedOpsCatalogReader persistedOpsCatalogReader;

    /// <summary> Initializes a new instance of the <see cref="ReadIndexValidationCatalogResolver" /> class. </summary>
    /// <param name="persistedOpsCatalogReader"> The persisted ops-catalog reader dependency. </param>
    public ReadIndexValidationCatalogResolver (
        IPersistedOpsCatalogReader persistedOpsCatalogReader)
    {
        this.persistedOpsCatalogReader = persistedOpsCatalogReader ?? throw new ArgumentNullException(nameof(persistedOpsCatalogReader));
    }

    /// <inheritdoc />
    public async ValueTask<ReadIndexValidationCatalogResolutionResult> Resolve (
        ResolvedUnityProjectContext unityProject,
        ReadIndexMode readIndexMode,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);

        if (readIndexMode == ReadIndexMode.Disabled)
        {
            return ReadIndexValidationCatalogResolutionResult.Success(
                RequestStaticValidationCatalog.Unavailable,
                CreateReadIndexMiss(ReadIndexDisabledReason));
        }

        var persistedCatalogResult = await persistedOpsCatalogReader.Read(
                unityProject,
                cancellationToken)
            .ConfigureAwait(false);
        if (!persistedCatalogResult.IsSuccess)
        {
            return HandlePersistedCatalogReadFailure(
                persistedCatalogResult.ReadFailure!,
                readIndexMode);
        }

        var freshness = persistedCatalogResult.Freshness!.Value;
        var snapshot = persistedCatalogResult.Snapshot!;
        var freshnessResult = IndexFreshnessPolicy.ApplyModeConstraint(readIndexMode, freshness);
        if (!freshnessResult.IsSuccess)
        {
            return ReadIndexValidationCatalogResolutionResult.Failure(
                CreateReadIndexHit(
                    freshnessResult.Freshness,
                    snapshot.GeneratedAtUtc,
                    freshnessResult.Error!.Message),
                freshnessResult.Error.Code,
                freshnessResult.Error.Message);
        }

        return ReadIndexValidationCatalogResolutionResult.Success(
            RequestStaticValidationCatalog.Available(OperationDescriptorMapper.Map(snapshot.Operations, cancellationToken)),
            CreateReadIndexHit(
                freshness,
                snapshot.GeneratedAtUtc,
                fallbackReason: null));
    }

    private static ReadIndexValidationCatalogResolutionResult HandlePersistedCatalogReadFailure (
        PersistedOpsCatalogReadFailure failure,
        ReadIndexMode readIndexMode)
    {
        ArgumentNullException.ThrowIfNull(failure);

        if ((readIndexMode == ReadIndexMode.AllowStale)
            && failure.Kind == PersistedOpsCatalogReadFailureKind.Unavailable)
        {
            return ReadIndexValidationCatalogResolutionResult.Success(
                RequestStaticValidationCatalog.Unavailable,
                CreateReadIndexMiss(failure.Message));
        }

        return ReadIndexValidationCatalogResolutionResult.Failure(
            CreateReadIndexMiss(failure.Message),
            failure.ErrorCode,
            failure.Message);
    }

    private static ReadIndexInfo CreateReadIndexMiss (string fallbackReason)
    {
        return new ReadIndexInfo(
            Used: false,
            Hit: false,
            Source: ReadIndexInfoSource.Index,
            Freshness: IndexFreshness.Probable,
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
            Source: ReadIndexInfoSource.Index,
            Freshness: freshness,
            GeneratedAtUtc: generatedAtUtc,
            FallbackReason: fallbackReason);
    }
}
