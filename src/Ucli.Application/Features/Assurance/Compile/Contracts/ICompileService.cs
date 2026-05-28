using MackySoft.Ucli.Application.Shared.Execution.Progress;

namespace MackySoft.Ucli.Application.Features.Assurance.Compile.Contracts;

/// <summary> Executes compile assurance probes and produces compile claim packets. </summary>
internal interface ICompileService
{
    /// <summary> Executes one compile assurance command. </summary>
    /// <param name="input"> The compile command input. </param>
    /// <param name="progressSink"> The optional progress sink that receives public compile stream entries. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The compile execution result. </returns>
    ValueTask<CompileExecutionResult> ExecuteAsync (
        CompileCommandInput input,
        ICommandProgressSink? progressSink = null,
        CancellationToken cancellationToken = default);
}
