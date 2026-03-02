using MackySoft.Ucli.TestRun.Configuration;

namespace MackySoft.Ucli.TestRun.Artifacts;

/// <summary> Writes <c>meta.json</c> snapshots for test-run artifacts sessions. </summary>
internal interface ITestRunMetaStore
{
    /// <summary> Writes one metadata snapshot for a run session. </summary>
    /// <param name="configuration"> The resolved test-run configuration. </param>
    /// <param name="session"> The artifacts session. </param>
    /// <param name="finishedAtUtc"> The completion timestamp to persist. </param>
    void Write (
        ResolvedTestRunConfiguration configuration,
        ArtifactsSession session,
        DateTimeOffset finishedAtUtc);
}