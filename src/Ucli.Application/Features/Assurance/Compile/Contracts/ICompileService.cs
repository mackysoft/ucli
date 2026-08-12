using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.Progress;

namespace MackySoft.Ucli.Application.Features.Assurance.Compile.Contracts;

/// <summary> Executes compile assurance probes and produces compile claim packets. </summary>
internal interface ICompileService
{
    /// <summary> Starts compile through the caller-fixed Lifecycle Execution context. </summary>
    ValueTask<CompileExecutionResult> StartAsync (
        LifecycleExecutionStartInvocation invocation,
        ICommandProgressSink? progressSink = null,
        CancellationToken cancellationToken = default);

    /// <summary> Reconnects compile through the caller-fixed Lifecycle Execution context. </summary>
    ValueTask<CompileExecutionResult> ReconnectAsync (
        LifecycleExecutionReconnectInvocation invocation,
        ICommandProgressSink? progressSink = null,
        CancellationToken cancellationToken = default);

}
