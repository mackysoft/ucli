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

    public Func<int>? ProcessIdProvider { get; set; }

    public Func<DateTimeOffset?>? StartTimeUtcProvider { get; set; }

    public Func<ProcessTerminationPolicy, CancellationToken, Task<ProcessTerminationResult>>? TerminateHandler { get; set; }

    public Action? OnDispose { get; set; }

    public int ProcessId => ProcessIdProvider?.Invoke() ?? 1234;

    public DateTimeOffset? StartTimeUtc => StartTimeUtcProvider is null
        ? DateTimeOffset.UtcNow
        : StartTimeUtcProvider();

    public bool HasExited { get; private set; }

    public int? ExitCode => HasExited ? exitCode ?? 0 : null;

    public IReadOnlyList<WaitForExitInvocation> WaitForExitInvocations => waitForExitInvocations;

    public IReadOnlyList<TerminateInvocation> TerminateInvocations => terminateInvocations;

    public int DisposeCount { get; private set; }

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
        ProcessTerminationPolicy terminationPolicy,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(terminationPolicy);
        cancellationToken.ThrowIfCancellationRequested();
        terminateInvocations.Add(new TerminateInvocation(terminationPolicy, cancellationToken));
        if (TerminateHandler is not null)
        {
            return TerminateHandler(terminationPolicy, cancellationToken);
        }

        HasExited = true;
        return Task.FromResult(ProcessTerminationResult.GracefulExited);
    }

    public ValueTask DisposeAsync ()
    {
        DisposeCount++;
        OnDispose?.Invoke();
        return ValueTask.CompletedTask;
    }

    internal readonly record struct WaitForExitInvocation (CancellationToken CancellationToken);

    internal readonly record struct TerminateInvocation (
        ProcessTerminationPolicy TerminationPolicy,
        CancellationToken CancellationToken);
}
