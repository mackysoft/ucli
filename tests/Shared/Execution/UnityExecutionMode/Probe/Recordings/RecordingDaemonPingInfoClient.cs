using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.TestSupport;

internal sealed class RecordingDaemonPingInfoClient : IDaemonPingInfoClient
{
    private readonly Queue<object> responses = [];
    private readonly List<Invocation> invocations = [];

    private readonly TaskCompletionSource<object?> firstInvocationObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public RecordingDaemonPingInfoClient (params object[] responses)
    {
        foreach (var response in responses)
        {
            this.responses.Enqueue(response);
        }
    }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public Action? OnPingAndRead { get; set; }

    public Func<ResolvedUnityProjectContext, TimeSpan, string?, bool, CancellationToken, ValueTask<IpcPingResponse>>? PingAndReadHandler { get; set; }

    public Task WaitForFirstInvocationAsync (
        string description,
        TimeSpan timeout)
    {
        return TestAwaiter.WaitAsync(firstInvocationObserved.Task, description, timeout);
    }

    public ValueTask<IpcPingResponse> PingAndReadAsync (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        string? sessionToken = null,
        bool validateProjectFingerprint = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        cancellationToken.ThrowIfCancellationRequested();

        invocations.Add(new Invocation(
            unityProject,
            timeout,
            sessionToken,
            validateProjectFingerprint,
            cancellationToken));
        firstInvocationObserved.TrySetResult(null);
        OnPingAndRead?.Invoke();

        if (PingAndReadHandler is not null)
        {
            return PingAndReadHandler(
                unityProject,
                timeout,
                sessionToken,
                validateProjectFingerprint,
                cancellationToken);
        }

        if (responses.Count == 0)
        {
            throw new Xunit.Sdk.XunitException("No daemon ping response was configured.");
        }

        var response = responses.Dequeue();
        if (response is Exception exception)
        {
            throw exception;
        }

        return ValueTask.FromResult((IpcPingResponse)response);
    }

    internal readonly record struct Invocation (
        ResolvedUnityProjectContext UnityProject,
        TimeSpan Timeout,
        string? SessionToken,
        bool ValidateProjectFingerprint,
        CancellationToken CancellationToken);
}
