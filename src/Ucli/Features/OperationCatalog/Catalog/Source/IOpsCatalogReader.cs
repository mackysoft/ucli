using MackySoft.Ucli.Features.Requests.Shared.Execution;
using MackySoft.Ucli.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Shared.Configuration;
using MackySoft.Ucli.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Shared.Execution.Process;
using MackySoft.Ucli.Shared.Execution.Timeout;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.UnityIntegration.Project;

namespace MackySoft.Ucli.Features.OperationCatalog.Catalog.Source;

/// <summary> Reads the operation catalog. </summary>
internal interface IOpsCatalogReader
{
    /// <summary> Reads the operation catalog through the shared IPC execution path. </summary>
    /// <param name="project"> The resolved project context. </param>
    /// <param name="config"> The loaded uCLI configuration. </param>
    /// <param name="mode"> The normalized Unity execution mode. </param>
    /// <param name="timeout"> The resolved timeout budget for this catalog read. </param>
    /// <param name="failFast"> Whether the Unity-side lifecycle gate should fail immediately instead of waiting when readiness gating is required. </param>
    /// <param name="requireReadinessGate"> Whether the Unity-side readiness gate should be applied to this catalog read. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the read result. </returns>
    ValueTask<OpsCatalogFetchResult> Read (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UnityExecutionMode mode,
        TimeSpan timeout,
        bool failFast,
        bool requireReadinessGate,
        CancellationToken cancellationToken = default);
}