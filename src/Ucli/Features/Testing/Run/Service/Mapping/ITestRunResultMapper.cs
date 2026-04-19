using MackySoft.Ucli.Features.Testing.Run.Service.Pipeline;

namespace MackySoft.Ucli.Features.Testing.Run.Service.Mapping;

/// <summary> Maps pipeline outcomes into command-facing test-run service results. </summary>
internal interface ITestRunResultMapper
{
    /// <summary> Maps one execution pipeline result into service output. </summary>
    /// <param name="pipelineResult"> The execution pipeline result. </param>
    /// <returns> The mapped service output. </returns>
    TestRunServiceResult Map (TestRunExecutionPipelineResult pipelineResult);
}