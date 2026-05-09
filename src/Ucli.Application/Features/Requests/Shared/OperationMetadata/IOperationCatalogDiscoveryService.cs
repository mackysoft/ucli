using MackySoft.Ucli.Application.Shared.Configuration;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;

/// <summary> Discovers operation descriptors for one resolved Unity project. </summary>
internal interface IOperationCatalogDiscoveryService
{
    /// <summary> Discovers operation descriptors for the specified resolved Unity project. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="config"> The loaded configuration used to execute catalog discovery. </param>
    /// <param name="mode"> The optional Unity execution mode for discovery. </param>
    /// <param name="timeout"> The optional discovery timeout budget. When <see langword="null" />, the default <c>ops</c> timeout is used. </param>
    /// <param name="failFast"> Whether live catalog discovery should fail immediately instead of waiting for Unity readiness. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The discovered operation descriptors. </returns>
    ValueTask<IReadOnlyList<UcliOperationDescriptor>> DiscoverAsync (
        ResolvedUnityProjectContext unityProject,
        UcliConfig config,
        UnityExecutionMode mode = UnityExecutionMode.Auto,
        TimeSpan? timeout = null,
        bool failFast = false,
        CancellationToken cancellationToken = default);
}
