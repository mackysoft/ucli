using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Ipc;
using MackySoft.Ucli.Unity.Runtime;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class RefreshLifecycleExecutionStartAdmissionPolicyTests
    {
        [Test]
        [Category("Size.Small")]
        public void AdmitAsync_WhenEditorIsReady_AcceptsFailFastObservation ()
        {
            var readinessGate = new StubUnityEditorReadinessGate();
            var expectedObservation = readinessGate.CaptureObservation();
            var policy = new RefreshLifecycleExecutionStartAdmissionPolicy(
                readinessGate);

            var admission = policy.AdmitAsync(
                    DateTimeOffset.UtcNow.AddMinutes(1),
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            Assert.That(admission.IsAccepted, Is.True);
            Assert.That(admission.Observation, Is.SameAs(expectedObservation));
            Assert.That(admission.Error, Is.Null);
            Assert.That(readinessGate.CallCount, Is.EqualTo(1));
            Assert.That(readinessGate.LastFailFast, Is.True);
        }

        [Test]
        [Category("Size.Small")]
        public void AdmitAsync_WhenEditorIsBusy_ReturnsTypedFailFastRejection ()
        {
            var readinessGate = StubUnityEditorReadinessGate.CreatePending();
            var policy = new RefreshLifecycleExecutionStartAdmissionPolicy(
                readinessGate);

            var admission = policy.AdmitAsync(
                    DateTimeOffset.UtcNow.AddMinutes(1),
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            Assert.That(admission.IsAccepted, Is.False);
            Assert.That(admission.Observation, Is.Null);
            Assert.That(
                admission.Error.Code,
                Is.EqualTo(EditorLifecycleErrorCodes.EditorBusy));
            Assert.That(readinessGate.CallCount, Is.EqualTo(1));
            Assert.That(readinessGate.LastFailFast, Is.True);
        }

        [Test]
        [Category("Size.Small")]
        public void AdmitAsync_WhenDeadlineAlreadyPassed_RejectsWithoutObservingReadiness ()
        {
            var readinessGate = new StubUnityEditorReadinessGate();
            var policy = new RefreshLifecycleExecutionStartAdmissionPolicy(
                readinessGate);

            var admission = policy.AdmitAsync(
                    DateTimeOffset.UtcNow.AddMinutes(-1),
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            Assert.That(admission.IsAccepted, Is.False);
            Assert.That(
                admission.Error.Code,
                Is.EqualTo(IpcTransportErrorCodes.IpcTimeout));
            Assert.That(readinessGate.CallCount, Is.EqualTo(0));
        }

        [Test]
        [Category("Size.Small")]
        public async Task AdmitAsync_WhenDeadlinePassesDuringReadyObservation_RejectsReadyResult ()
        {
            var readinessGate = new BlockingReadyReadinessGate(
                new StubUnityEditorReadinessGate().CaptureObservation());
            var policy = new RefreshLifecycleExecutionStartAdmissionPolicy(
                readinessGate);
            var deadlineUtc = DateTimeOffset.UtcNow.AddMilliseconds(25);

            var admissionTask = policy.AdmitAsync(
                    deadlineUtc,
                    CancellationToken.None)
                .AsTask();
            await readinessGate.WaitObserved;
            while (DateTimeOffset.UtcNow <= deadlineUtc)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(5));
            }
            readinessGate.Release();
            var admission = await admissionTask;

            Assert.That(admission.IsAccepted, Is.False);
            Assert.That(
                admission.Error.Code,
                Is.EqualTo(IpcTransportErrorCodes.IpcTimeout));
            Assert.That(readinessGate.CallCount, Is.EqualTo(1));
            Assert.That(readinessGate.LastFailFast, Is.True);
        }

        private sealed class BlockingReadyReadinessGate :
            IUnityEditorReadinessGate
        {
            private readonly UnityEditorRuntimeObservation observation;

            private readonly TaskCompletionSource<bool> waitObserved =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource<
                UnityEditorExecutionReadinessResult> result =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            public BlockingReadyReadinessGate (
                UnityEditorRuntimeObservation observation)
            {
                this.observation = observation;
            }

            public int CallCount { get; private set; }

            public bool? LastFailFast { get; private set; }

            public Task WaitObserved => waitObserved.Task;

            public UnityEditorRuntimeObservation CaptureObservation ()
            {
                return observation;
            }

            public Task<UnityEditorExecutionReadinessResult>
                EnsureExecutionReadyAsync (
                    bool failFast,
                    CancellationToken cancellationToken = default,
                    bool allowPlayMode = false)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CallCount++;
                LastFailFast = failFast;
                waitObserved.TrySetResult(true);
                return result.Task;
            }

            public void Release ()
            {
                result.TrySetResult(
                    UnityEditorExecutionReadinessResult.Ready(
                        observation));
            }
        }
    }
}
