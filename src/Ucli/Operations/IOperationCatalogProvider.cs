namespace MackySoft.Ucli.Operations;

/// <summary> Provides operation descriptor values to the operation catalog. </summary>
internal interface IOperationCatalogProvider
{
    /// <summary> Asynchronously gets operation descriptor values used for catalog construction. </summary>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the operation descriptor collection. </returns>
    ValueTask<IReadOnlyList<UcliOperationDescriptor>> GetOperations (CancellationToken cancellationToken = default);
}