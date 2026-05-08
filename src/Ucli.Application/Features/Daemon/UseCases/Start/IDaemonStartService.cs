using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Application.Features.Daemon.UseCases.Start;

/// <summary> Executes daemon-start workflow and returns normalized command output values. </summary>
internal interface IDaemonStartService
{
    /// <summary> Executes one daemon-start workflow. </summary>
    /// <param name="projectPath"> The optional <c>--projectPath</c> option value. </param>
    /// <param name="timeoutMilliseconds"> The optional normalized timeout value in milliseconds. </param>
    /// <param name="editorMode"> The optional normalized <c>--editorMode</c> value. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon-start execution result. </returns>
    ValueTask<DaemonStartExecutionResult> Start (
        string? projectPath,
        int? timeoutMilliseconds,
        DaemonEditorMode? editorMode,
        CancellationToken cancellationToken = default);
}
