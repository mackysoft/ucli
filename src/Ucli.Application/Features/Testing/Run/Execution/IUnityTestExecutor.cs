using MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;
using MackySoft.Ucli.Application.Features.Testing.Run.Configuration;

namespace MackySoft.Ucli.Application.Features.Testing.Run.Execution;

/// <summary> Executes Unity test process and verifies required run artifacts. </summary>
internal interface IUnityTestExecutor
{
    /// <summary> Executes one Unity test run. </summary>
    /// <param name="configuration"> The resolved test-run configuration. </param>
    /// <param name="artifactPaths"> The run artifact paths. </param>
    /// <param name="timeout"> The execution timeout for one run. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by the caller. </param>
    /// <returns> A task that resolves to the Unity test execution result. </returns>
    ValueTask<UnityTestExecutionResult> ExecuteAsync (
        ResolvedTestRunConfiguration configuration,
        ArtifactPaths artifactPaths,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
