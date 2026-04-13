using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Operations;

/// <summary> Provides lookup and listing over registered operation metadata. </summary>
internal interface IOperationCatalog
{
    /// <summary> Asynchronously gets one operation descriptor by operation name. </summary>
    /// <param name="name"> The operation name to resolve. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns>
    /// <para> A task that resolves to the operation descriptor. </para>
    /// <para> Returns <see langword="null" /> when the operation does not exist. </para>
    /// </returns>
    ValueTask<UcliOperationDescriptor?> Get (string name, CancellationToken cancellationToken = default);

    /// <summary> Asynchronously gets all registered operation descriptors. </summary>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the descriptor list ordered by operation name. </returns>
    ValueTask<IReadOnlyList<UcliOperationDescriptor>> GetAll (CancellationToken cancellationToken = default);

    /// <summary> Asynchronously gets all registered operation descriptors for the specified resolved Unity project. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="config"> The loaded configuration used to execute catalog discovery. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the descriptor list ordered by operation name. </returns>
    ValueTask<IReadOnlyList<UcliOperationDescriptor>> GetAll (
        ResolvedUnityProjectContext unityProject,
        UcliConfig config,
        UnityExecutionMode mode = UnityExecutionMode.Auto,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);
}