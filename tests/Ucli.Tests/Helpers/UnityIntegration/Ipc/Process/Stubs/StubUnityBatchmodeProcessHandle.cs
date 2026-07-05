using MackySoft.Ucli.UnityIntegration.Ipc.Process;

namespace MackySoft.Ucli.Tests.Helpers.Process;

internal sealed class StubUnityBatchmodeProcessHandle : IUnityBatchmodeProcessHandle
{
    private readonly int? exitCode;

    private readonly Func<CancellationToken, Task>? waitForExitBehavior;

    private readonly List<WaitForExitInvocation> waitForExitInvocations = [];
    private readonly List<TerminateInvocation> terminateInvocations = [];

    public StubUnityBatchmodeProcessHandle (
        bool hasExited = false,
        int? exitCode = null,
        Func<CancellationToken, Task>? waitForExitBehavior = null)
    {
        HasExited = hasExited;
        this.exitCode = exitCode;
        this.waitForExitBehavior = waitForExitBehavior;
    }

    public int ProcessId => 1234;

    public DateTimeOffset? StartTimeUtc => DateTimeOffset.UtcNow;

    public bool HasExited { get; private set; }

    public int? ExitCode => HasExited ? exitCode ?? 0 : null;

    public IReadOnlyList<WaitForExitInvocation> WaitForExitInvocations => waitForExitInvocations;

    public IReadOnlyList<TerminateInvocation> TerminateInvocations => terminateInvocations;

    public static StubUnityBatchmodeProcessHandle CreateNonExiting ()
    {
        return new StubUnityBatchmodeProcessHandle(
            waitForExitBehavior: static async cancellationToken =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            });
    }

    public async Task WaitForExitAsync (CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        waitForExitInvocations.Add(new WaitForExitInvocation(cancellationToken));
        if (waitForExitBehavior != null)
        {
            await waitForExitBehavior(cancellationToken).ConfigureAwait(false);
        }

        HasExited = true;
    }

    public Task<ProcessTerminationResult> TerminateAsync (
        ProcessTerminationPolicy? terminationPolicy = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        terminateInvocations.Add(new TerminateInvocation(terminationPolicy, cancellationToken));
        HasExited = true;
        return Task.FromResult(ProcessTerminationResult.GracefulExited);
    }

    public ValueTask DisposeAsync ()
    {
        return ValueTask.CompletedTask;
    }

    internal readonly record struct WaitForExitInvocation (CancellationToken CancellationToken);

    internal readonly record struct TerminateInvocation (
        ProcessTerminationPolicy? TerminationPolicy,
        CancellationToken CancellationToken);
}
