using MackySoft.Ucli.TestRun.Artifacts;
using MackySoft.Ucli.TestRun.Configuration;

namespace MackySoft.Ucli.TestRun.Execution;

/// <summary> Builds Unity batchmode command arguments for test execution. </summary>
internal interface IUnityCommandBuilder
{
    /// <summary> Builds one Unity command argument list from resolved run configuration and artifact paths. </summary>
    /// <param name="configuration"> The resolved test-run configuration. </param>
    /// <param name="artifactPaths"> The run artifact paths. </param>
    /// <returns> The command argument list. </returns>
    IReadOnlyList<string> BuildArguments (
        ResolvedTestRunConfiguration configuration,
        ArtifactPaths artifactPaths);
}