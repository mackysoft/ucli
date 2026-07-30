using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;

/// <summary> Resolves read-index backed static-validation catalogs and emitted read-index payload information. </summary>
internal interface IReadIndexValidationCatalogResolver
{
    /// <summary> Resolves one static-validation catalog from persisted read-index artifacts. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="readIndexMode"> The effective read-index mode. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to metadata and read-index output information. </returns>
    ValueTask<ReadIndexValidationCatalogResolutionResult> ResolveAsync (
        ResolvedUnityProjectContext unityProject,
        ReadIndexMode readIndexMode,
        CancellationToken cancellationToken = default);

    /// <summary> Resolves one operation descriptor from the catalog persisted with a specific read-index generation. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="readIndexMode"> The effective read-index mode. </param>
    /// <param name="expectedGeneration"> The generation recorded by the read-index result that will consume the descriptor. </param>
    /// <param name="operationName"> The operation name to resolve. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the descriptor from the matching persisted generation. </returns>
    /// <exception cref="OperationCatalogLoadException">
    /// Thrown when the persisted catalog cannot be read, does not match <paramref name="expectedGeneration" />,
    /// or does not contain <paramref name="operationName" />.
    /// </exception>
    ValueTask<UcliOperationDescriptor> ResolveOperationAsync (
        ResolvedUnityProjectContext unityProject,
        ReadIndexMode readIndexMode,
        DateTimeOffset? expectedGeneration,
        string operationName,
        CancellationToken cancellationToken = default);
}
