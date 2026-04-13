using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Ops;

/// <summary> Reads the operation catalog. </summary>
internal interface IOpsCatalogReader
{
    /// <summary> Reads the operation catalog through the shared IPC execution path. </summary>
    /// <param name="project"> The resolved project context. </param>
    /// <param name="config"> The loaded uCLI configuration. </param>
    /// <param name="mode"> The normalized Unity execution mode. </param>
    /// <param name="timeout"> The resolved timeout budget for this catalog read. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the read result. </returns>
    ValueTask<OpsCatalogFetchResult> Read (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UnityExecutionMode mode,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}