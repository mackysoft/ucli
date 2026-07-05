using MackySoft.Ucli.Application.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Application.Shared.Execution.Progress;

namespace MackySoft.Tests;

internal sealed class RecordingDaemonStartService : IDaemonStartService
{
    private readonly DaemonStartExecutionResult result;
    private readonly List<Invocation> invocations = [];

    private readonly Func<ICommandProgressSink?, CancellationToken, ValueTask>? progressHandler;

    public RecordingDaemonStartService (
        DaemonStartExecutionResult result,
        Func<ICommandProgressSink?, CancellationToken, ValueTask>? progressHandler = null)
    {
        this.result = result ?? throw new ArgumentNullException(nameof(result));
        this.progressHandler = progressHandler;
    }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<DaemonStartExecutionResult> StartAsync (
        string? projectPath,
        int? timeoutMilliseconds,
        DaemonEditorMode? editorMode,
        DaemonStartupBlockedProcessPolicy onStartupBlocked,
        ICommandProgressSink? progressSink = null,
        CancellationToken cancellationToken = default)
    {
        invocations.Add(new Invocation(
            projectPath,
            timeoutMilliseconds,
            editorMode,
            onStartupBlocked,
            progressSink,
            cancellationToken));
        if (progressHandler != null)
        {
            return StartCoreAsync(progressSink, cancellationToken);
        }

        return ValueTask.FromResult(result);
    }

    private async ValueTask<DaemonStartExecutionResult> StartCoreAsync (
        ICommandProgressSink? progressSink,
        CancellationToken cancellationToken)
    {
        await progressHandler!(progressSink, cancellationToken).ConfigureAwait(false);
        return result;
    }

    public readonly record struct Invocation (
        string? ProjectPath,
        int? TimeoutMilliseconds,
        DaemonEditorMode? EditorMode,
        DaemonStartupBlockedProcessPolicy OnStartupBlocked,
        ICommandProgressSink? ProgressSink,
        CancellationToken CancellationToken);
}
