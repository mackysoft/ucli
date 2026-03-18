using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Unity.Ipc;

namespace MackySoft.Ucli.Unity.Tests
{
    internal sealed class StubUnityEditorReadinessGate : IUnityEditorReadinessGate
    {
        private readonly TaskCompletionSource<bool>? completionSource;

        private readonly TaskCompletionSource<bool> waitObserved =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public StubUnityEditorReadinessGate ()
            : this(null)
        {
        }

        private StubUnityEditorReadinessGate (TaskCompletionSource<bool>? completionSource)
        {
            this.completionSource = completionSource;
        }

        public int CallCount { get; private set; }

        public Task WaitObserved => waitObserved.Task;

        public static StubUnityEditorReadinessGate CreatePending ()
        {
            return new StubUnityEditorReadinessGate(new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));
        }

        public void Release ()
        {
            completionSource?.TrySetResult(true);
        }

        public Task WaitUntilReady (CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            waitObserved.TrySetResult(true);
            return completionSource?.Task ?? Task.CompletedTask;
        }
    }
}