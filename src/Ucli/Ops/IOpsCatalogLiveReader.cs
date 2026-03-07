using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Ops;

/// <summary> Reads the live operation catalog from Unity. </summary>
internal interface IOpsCatalogLiveReader
{
    /// <summary> Reads the live operation catalog through the resolved Unity execution target. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="target"> The resolved execution target. </param>
    /// <param name="timeout"> The timeout applied to the read operation. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the live-read result. </returns>
    ValueTask<OpsCatalogLiveReadResult> Read (
        ResolvedUnityProjectContext unityProject,
        UnityExecutionTarget target,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}