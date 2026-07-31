using MackySoft.Ucli.Application.Shared.Execution.Progress;

namespace MackySoft.Ucli.Application.Features.Assurance.Compile.Contracts;

/// <summary> Executes compile assurance probes and produces compile claim packets. </summary>
internal interface ICompileService
{
    /// <summary> Executes one typed compile assurance workflow. </summary>
    /// <param name="input"> The normalized compile input. </param>
    /// <param name="progressSink"> The optional progress sink that receives public compile stream entries. </param>
    /// <param name="cancellationToken"> The application caller's wait cancellation token. </param>
    /// <returns> The compile execution result. </returns>
    ValueTask<CompileExecutionResult> ExecuteAsync (
        CompileCommandInput input,
        ICommandProgressSink? progressSink = null,
        CancellationToken cancellationToken = default);

    /// <summary> Reconnects to one previously published compile Lifecycle Execution. </summary>
    /// <param name="input"> The normalized caller wait and execution-target input. </param>
    /// <param name="lifecycleExecutionRef"> The published compile execution reference to reconnect. </param>
    /// <param name="progressSink"> The optional progress sink that receives completion entries. </param>
    /// <param name="cancellationToken"> The application caller's wait cancellation token. </param>
    /// <returns> The compile execution result collected from the original execution. </returns>
    ValueTask<CompileExecutionResult> ReconnectAsync (
        CompileCommandInput input,
        ExecutionRef lifecycleExecutionRef,
        ICommandProgressSink? progressSink = null,
        CancellationToken cancellationToken = default);
}
