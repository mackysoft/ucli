using MackySoft.Ucli.TestRun.Artifacts;
using MackySoft.Ucli.TestRun.Configuration;

namespace MackySoft.Ucli.TestRun.Execution;

/// <summary> Executes Unity test runs through daemon IPC transport. </summary>
internal interface IDaemonTestRunClient
{
    /// <summary> Executes one Unity test run through daemon IPC. </summary>
    /// <param name="configuration"> The resolved test-run configuration. </param>
    /// <param name="artifactPaths"> The run artifact paths. </param>
    /// <param name="timeout"> The IPC timeout used for one daemon request. </param>
    /// <param name="waitUntilReady"> Whether daemon execution may wait for lifecycle readiness before failing. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by caller. </param>
    /// <returns> A task that resolves to the Unity test execution result. </returns>
    ValueTask<UnityTestExecutionResult> Execute (
        ResolvedTestRunConfiguration configuration,
        ArtifactPaths artifactPaths,
        TimeSpan timeout,
        bool waitUntilReady,
        CancellationToken cancellationToken = default);
}