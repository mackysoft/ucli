using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Runtime;
using MackySoft.Ucli.Unity.ScreenshotCapture;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityEditorScreenshotCaptureBackendRoutingTests
    {
        private const string CleanupFailure = "Expected orphan cleanup failure.";

        [Test]
        [Category("Size.Small")]
        public void TryPrepareRequestedResolutionTransaction_WithSceneTarget_DoesNotRequireCleanup ()
        {
            var cleaner = new SpyResolutionOrphanCleaner(isSuccess: false);
            var backend = CreateBackend(cleaner);

            var result = backend.TryPrepareRequestedResolutionTransaction(
                CreateRequest(IpcScreenshotTargetNames.Scene),
                out var failureResult);

            Assert.That(result, Is.True);
            Assert.That(failureResult, Is.Null);
            Assert.That(cleaner.CallCount, Is.Zero);
        }

        [Test]
        [Category("Size.Small")]
        public void TryPrepareRequestedResolutionTransaction_WithCurrentGameTarget_DoesNotRequireCleanup ()
        {
            var cleaner = new SpyResolutionOrphanCleaner(isSuccess: false);
            var backend = CreateBackend(cleaner);

            var result = backend.TryPrepareRequestedResolutionTransaction(
                CreateRequest(IpcScreenshotTargetNames.Game),
                out var failureResult);

            Assert.That(result, Is.True);
            Assert.That(failureResult, Is.Null);
            Assert.That(cleaner.CallCount, Is.Zero);
        }

        [Test]
        [Category("Size.Small")]
        public void TryPrepareRequestedResolutionTransaction_WithRequestedGame_WhenCleanupFails_FailsClosed ()
        {
            var cleaner = new SpyResolutionOrphanCleaner(isSuccess: false);
            var backend = CreateBackend(cleaner);

            var result = backend.TryPrepareRequestedResolutionTransaction(
                CreateRequest(
                    IpcScreenshotTargetNames.Game,
                    requestedWidth: 640,
                    requestedHeight: 360),
                out var failureResult);

            Assert.That(result, Is.False);
            Assert.That(cleaner.CallCount, Is.EqualTo(1));
            Assert.That(failureResult.IsSuccess, Is.False);
            Assert.That(failureResult.ErrorCode, Is.EqualTo(ScreenshotErrorCodes.ScreenshotCaptureUnsupported));
            Assert.That(failureResult.ErrorMessage, Does.Contain(CleanupFailure));
        }

        private static UnityEditorScreenshotCaptureBackend CreateBackend (
            IUnityScreenshotResolutionOrphanCleaner cleaner)
        {
            return new UnityEditorScreenshotCaptureBackend(
                new UnityEditorScreenshotReflectionAdapter(),
                new StubUnityEditorUpdateAwaiter(),
                cleaner);
        }

        private static IpcScreenshotCaptureRequest CreateRequest (
            string target,
            int? requestedWidth = null,
            int? requestedHeight = null)
        {
            return new IpcScreenshotCaptureRequest(
                target,
                requestedWidth,
                requestedHeight,
                StagingPath: "/tmp/ucli-routing-test/capture.rgba",
                TimeoutMilliseconds: 5000);
        }

        private sealed class SpyResolutionOrphanCleaner : IUnityScreenshotResolutionOrphanCleaner
        {
            private readonly bool isSuccess;

            public SpyResolutionOrphanCleaner (bool isSuccess)
            {
                this.isSuccess = isSuccess;
            }

            public int CallCount { get; private set; }

            public bool TryCleanup (out string errorMessage)
            {
                CallCount++;
                errorMessage = isSuccess ? null : CleanupFailure;
                return isSuccess;
            }
        }

        private sealed class StubUnityEditorUpdateAwaiter : IUnityEditorUpdateAwaiter
        {
            public Task WaitForNextUpdateAsync (CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }
        }
    }
}
