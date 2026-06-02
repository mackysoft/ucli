using MackySoft.Ucli.Application.Shared.Execution.Progress;

namespace MackySoft.Ucli.Application.Features.Daemon.UseCases.Start;

/// <summary> Executes daemon-start workflow and returns normalized command output values. </summary>
internal interface IDaemonStartService
{
    /// <summary> Executes one daemon-start workflow. </summary>
    /// <param name="projectPath"> The optional <c>--projectPath</c> option value. </param>
    /// <param name="timeoutMilliseconds"> The optional normalized timeout value in milliseconds. </param>
    /// <param name="editorMode"> The optional normalized <c>--editorMode</c> value. </param>
    /// <param name="onStartupBlocked"> The normalized <c>--onStartupBlocked</c> value. </param>
    /// <param name="progressSink"> The optional command-neutral sink that receives host-visible daemon-start progress entries. When <see langword="null" />, no progress entries are emitted. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon-start execution result. </returns>
    ValueTask<DaemonStartExecutionResult> StartAsync (
        string? projectPath,
        int? timeoutMilliseconds,
        DaemonEditorMode? editorMode,
        DaemonStartupBlockedProcessPolicy onStartupBlocked,
        ICommandProgressSink? progressSink = null,
        CancellationToken cancellationToken = default);
}
