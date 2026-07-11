using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Unity.ScreenshotCapture.GameView.Resolution;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityGameViewResolutionAdapterTests
    {
        [Test]
        [Category("Size.Small")]
        public void TryBegin_WhenTargetIsNotExactGameView_RejectsBeforeOrphanCleanup ()
        {
            var cleaner = new RejectingOrphanCleaner();
            var window = EditorWindow.CreateInstance<SceneView>();
            try
            {
                var result = new UnityGameViewResolutionAdapter(cleaner).TryBegin(
                    window,
                    width: 321,
                    height: 197,
                    AcceptPresentationRecoveryOwnership,
                    out var lease,
                    out var errorCode,
                    out var errorMessage);

                Assert.That(result, Is.False);
                Assert.That(lease, Is.Null);
                Assert.That(errorCode, Is.EqualTo(ScreenshotErrorCodes.ScreenshotCaptureUnsupported));
                Assert.That(errorMessage, Does.Contain("exact Unity GameView"));
                Assert.That(cleaner.CallCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

        private static bool AcceptPresentationRecoveryOwnership (out string errorMessage)
        {
            errorMessage = null;
            return true;
        }

        private sealed class RejectingOrphanCleaner : IUnityScreenshotResolutionOrphanCleaner
        {
            public int CallCount { get; private set; }

            public bool TryCleanup (out string errorMessage)
            {
                CallCount++;
                errorMessage = "Expected test rejection.";
                return false;
            }
        }
    }
}
