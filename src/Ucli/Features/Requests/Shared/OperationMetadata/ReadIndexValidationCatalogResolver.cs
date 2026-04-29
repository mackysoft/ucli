using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.OperationCatalog.Catalog.Source;

namespace MackySoft.Ucli.Features.Requests.Shared.OperationMetadata;

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
            return HandleSnapshotReadFailure(
                persistedCatalogResult.ErrorCode!,
                persistedCatalogResult.ErrorMessage!,
                readIndexMode);
        }

        var freshness = persistedCatalogResult.Freshness!.Value;
        var generatedAtUtc = persistedCatalogResult.GeneratedAtUtc!.Value;
        var freshnessResult = IndexFreshnessPolicy.ApplyModeConstraint(readIndexMode, freshness);
        if (!freshnessResult.IsSuccess)
        {
            return ReadIndexValidationCatalogResolutionResult.Failure(
                CreateReadIndexHit(
                    freshnessResult.Freshness,
                    generatedAtUtc,
                    freshnessResult.Error!.Message),
                freshnessResult.Error.Code,
                freshnessResult.Error.Message);
        }

        IReadOnlyList<UcliOperationDescriptor> operations;
        try
        {
            operations = OperationDescriptorMapper.Map(persistedCatalogResult.Entries!, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            var message = $"Index contract file 'ops.catalog.json' is malformed. {exception.Message}";
            return ReadIndexValidationCatalogResolutionResult.Failure(
                CreateReadIndexMiss(message),
                IpcErrorCodes.ReadIndexFormatInvalid,
                message);
        }

        return ReadIndexValidationCatalogResolutionResult.Success(
            RequestStaticValidationCatalog.Available(operations),
            CreateReadIndexHit(
                freshness,
                generatedAtUtc,
                fallbackReason: null));
    }

    private static ReadIndexValidationCatalogResolutionResult HandleSnapshotReadFailure (
        string errorCode,
        string errorMessage,
        ReadIndexMode readIndexMode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        if ((readIndexMode == ReadIndexMode.AllowStale)
            && string.Equals(errorCode, IpcErrorCodes.ReadIndexBootstrapFailed, StringComparison.Ordinal))
        {
            return ReadIndexValidationCatalogResolutionResult.Success(
                RequestStaticValidationCatalog.Unavailable,
                CreateReadIndexMiss(errorMessage));
        }

        return ReadIndexValidationCatalogResolutionResult.Failure(
            CreateReadIndexMiss(errorMessage),
            errorCode,
            errorMessage);
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
