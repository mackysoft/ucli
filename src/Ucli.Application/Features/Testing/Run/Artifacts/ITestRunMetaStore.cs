using MackySoft.Ucli.Application.Features.Testing.Run.Configuration;

namespace MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;

/// <summary> Writes <c>meta.json</c> snapshots for test-run artifacts sessions. </summary>
internal interface ITestRunMetaStore
{
    /// <summary> Writes one metadata snapshot for a run session. </summary>
    /// <param name="configuration"> The resolved test-run configuration. </param>
    /// <param name="session"> The artifacts session. </param>
    /// <param name="finishedAtUtc"> The completion timestamp to persist. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by caller. </param>
    /// <returns> A task that completes when metadata writing is finished. </returns>
    ValueTask WriteAsync (
        ResolvedTestRunConfiguration configuration,
        ArtifactsSession session,
        DateTimeOffset finishedAtUtc,
        CancellationToken cancellationToken = default);
}
