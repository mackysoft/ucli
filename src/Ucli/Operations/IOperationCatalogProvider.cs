using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Operations;

/// <summary> Provides operation descriptor values to the operation catalog. </summary>
internal interface IOperationCatalogProvider
{
    /// <summary> Asynchronously gets operation descriptor values used for catalog construction. </summary>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the operation descriptor collection. </returns>
    ValueTask<IReadOnlyList<UcliOperationDescriptor>> GetOperations (CancellationToken cancellationToken = default);

    /// <summary> Asynchronously gets operation descriptor values for the specified resolved Unity project. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="config"> The loaded configuration used to execute catalog discovery. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the operation descriptor collection. </returns>
    ValueTask<IReadOnlyList<UcliOperationDescriptor>> GetOperations (
        ResolvedUnityProjectContext unityProject,
        UcliConfig config,
        CancellationToken cancellationToken = default);
}