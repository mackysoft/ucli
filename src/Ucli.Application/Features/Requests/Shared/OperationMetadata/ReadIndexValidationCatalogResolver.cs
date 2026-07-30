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
    public async ValueTask<ReadIndexValidationCatalogResolutionResult> ResolveAsync (
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

        var persistedCatalogResult = await persistedOpsCatalogReader.ReadAsync(
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

    /// <inheritdoc />
    public async ValueTask<UcliOperationDescriptor> ResolveOperationAsync (
        ResolvedUnityProjectContext unityProject,
        ReadIndexMode readIndexMode,
        DateTimeOffset? expectedGeneration,
        string operationName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        var resolution = await ResolveAsync(
                unityProject,
                readIndexMode,
                cancellationToken)
            .ConfigureAwait(false);
        if (!resolution.IsSuccess)
        {
            throw OperationCatalogLoadException.Create(
                ApplicationFailure.FromCode(
                    resolution.ErrorCode,
                    resolution.ErrorMessage!),
                "Persisted operation catalog could not be resolved.");
        }

        if (expectedGeneration is null
            || resolution.ReadIndex.GeneratedAtUtc != expectedGeneration)
        {
            throw OperationCatalogLoadException.Create(
                ApplicationFailure.InternalError(
                    $"Operation catalog generation does not match the read-index result generation for '{operationName}'."),
                "Read-index operation metadata is inconsistent.");
        }

        if (!resolution.Catalog.IsAvailable
            || !resolution.Catalog.OperationsByName.TryGetValue(operationName, out var descriptor))
        {
            throw OperationCatalogLoadException.Create(
                ApplicationFailure.InternalError(
                    $"Persisted operation catalog does not contain '{operationName}'."),
                "Read-index operation metadata is inconsistent.");
        }

        return descriptor;
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
