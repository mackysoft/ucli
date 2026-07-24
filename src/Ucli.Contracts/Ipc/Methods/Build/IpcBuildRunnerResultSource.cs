
namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines build runner terminal result source literals. </summary>
[VocabularyDefinition]
public enum IpcBuildRunnerResultSource
{
    /// <summary> The terminal result was normalized from Unity BuildPipeline BuildReport. </summary>
    [VocabularyText("buildPipelineBuildReport")]
    BuildPipelineBuildReport = 1,

    /// <summary> The terminal result was returned by <c>UcliBuildRunnerResult</c>. </summary>
    [VocabularyText("ucliBuildRunnerResult")]
    UcliBuildRunnerResult = 2,
}
