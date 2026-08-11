using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class LifecycleExecutionStartObserverGateTests
{
    [Fact]
    public async Task ObserveAsync_WhenSameStartIsRecovered_InvokesObserverOnlyOnce ()
    {
        var observer = new RecordingObserver(
            LifecycleExecutionStartObservation.Observed.Instance);
        var gate = new LifecycleExecutionStartObserverGate(observer);
        var start = CreateStart(LifecycleExecutionKind.Refresh);

        var first = await gate.ObserveAsync(start);
        var recovered = await gate.ObserveAsync(start);

        Assert.Same(LifecycleExecutionStartObservation.Observed.Instance, first);
        Assert.Same(first, recovered);
        Assert.Equal(1, observer.CallCount);
    }

    [Fact]
    public async Task ObserveAsync_WhenObserverRejects_RetainsSameRejectedOutcomeOnRecovery ()
    {
        var rejection = new LifecycleExecutionStartObservation.Rejected(
            ApplicationFailure.InternalError("Program Run durable start persistence failed."));
        var observer = new RecordingObserver(rejection);
        var gate = new LifecycleExecutionStartObserverGate(observer);
        var start = CreateStart(LifecycleExecutionKind.Compile);

        var first = await gate.ObserveAsync(start);
        var recovered = await gate.ObserveAsync(start);

        Assert.Same(rejection, first);
        Assert.Same(first, recovered);
        Assert.Equal(1, observer.CallCount);
    }

    private sealed class RecordingObserver : ILifecycleExecutionStartObserver
    {
        private readonly LifecycleExecutionStartObservation outcome;

        public RecordingObserver (LifecycleExecutionStartObservation outcome)
        {
            this.outcome = outcome;
        }

        public int CallCount { get; private set; }

        public ValueTask<LifecycleExecutionStartObservation> ObserveAsync (
            LifecycleExecutionStartBinding start)
        {
            ArgumentNullException.ThrowIfNull(start);
            CallCount++;
            return ValueTask.FromResult(outcome);
        }
    }

    private static LifecycleExecutionStartBinding CreateStart (
        LifecycleExecutionKind kind)
    {
        return LifecycleExecutionIpcTestResponseFactory.CreateStartBinding(
            new IpcLifecycleExecutionStartRequest(
                kind,
                Guid.NewGuid(),
                LifecycleExecutionDefinitionDigest.Calculate(
                    new LifecycleExecutionDefinition(kind)),
                DateTimeOffset.UtcNow.AddMinutes(1),
                DateTimeOffset.UtcNow));
    }
}
