using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Features.Requests.Shared.OperationMetadata;

/// <summary> Resolves read-index backed static-validation catalogs and emitted read-index payload information. </summary>
internal interface IReadIndexValidationCatalogResolver
{
    /// <summary> Resolves one static-validation catalog from persisted read-index artifacts. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="readIndexMode"> The effective read-index mode. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to metadata and read-index output information. </returns>
    ValueTask<ReadIndexValidationCatalogResolutionResult> Resolve (
        ResolvedUnityProjectContext unityProject,
        ReadIndexMode readIndexMode,
        CancellationToken cancellationToken = default);
}
