using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Runtime;
using MackySoft.Ucli.Unity.ScreenshotCapture.GameView;
using MackySoft.Ucli.Unity.ScreenshotCapture.GameView.Resolution;
using MackySoft.Ucli.Unity.ScreenshotCapture.Pixels;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityGameViewScreenshotCaptureTests
    {
        private RenderTexture sourceTexture;

        private FakeGameView gameView;

        [SetUp]
        public void SetUp ()
        {
            UnityScreenshotResolutionLeaseRegistry.ClearForTests();
            GameViewResolutionPresentationRecovery.ClearForTests();
        }

        [TearDown]
        public void TearDown ()
        {
            RenderTexture.active = null;
            if (sourceTexture != null)
            {
                UnityEngine.Object.DestroyImmediate(sourceTexture);
            }

            if (gameView != null)
            {
                UnityEngine.Object.DestroyImmediate(gameView);
            }

            UnityScreenshotResolutionLeaseRegistry.ClearForTests();
            GameViewResolutionPresentationRecovery.ClearForTests();
        }

        [Test]
        [Category("Size.Small")]
        public void CaptureAsync_CurrentSurface_WhenImmediateRepaintFails_ReturnsUnsupported ()
        {
            const int width = 4;
            const int height = 3;
            var source = CreateSource(width, height, backingScale: 1f);
            var presentationAdapter = new FailingRepaintPresentationAdapter(source);
            var editorUpdateAwaiter = new CountingEditorUpdateAwaiter();
            var capture = new UnityGameViewScreenshotCapture(
                presentationAdapter,
                new UnityGameViewResolutionAdapter(new SuccessfulOrphanCleaner()),
                editorUpdateAwaiter);

            var result = capture.CaptureAsync(
                    new IpcScreenshotCaptureRequest(
                        Guid.Parse("1e556a0d-3b9c-4be6-bae3-c1142637afcb"),
                        IpcScreenshotTarget.Game,
                        RequestedWidth: null,
                        RequestedHeight: null),
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(ScreenshotErrorCodes.ScreenshotCaptureUnsupported));
            Assert.That(result.ErrorMessage, Is.EqualTo("Fresh presentation unavailable."));
            Assert.That(editorUpdateAwaiter.WaitCount, Is.GreaterThan(0));
            Assert.That(presentationAdapter.ImmediateRepaintCount, Is.GreaterThan(0));
        }

        [Test]
        [Category("Size.Small")]
        public void CaptureAsync_CurrentSurface_WhenMappingChangesDuringImmediateRepaint_ReturnsUnsupported ()
        {
            const int width = 4;
            const int height = 3;
            var initialSource = CreateSource(width, height, backingScale: 1f);
            var changedSource = new GameViewPresentationSource(
                gameView,
                sourceTexture,
                width,
                height,
                BackingScale: 1.25f,
                TargetDisplay: 0,
                TargetInView: new Rect(0f, 0f, width, height),
                DeviceFlippedTargetInView: new Rect(0f, 0f, width, height),
                SourceUvTransform: new Vector4(1f, 1f, 0f, 0f));
            var presentationAdapter = new MappingChangingPresentationAdapter(
                initialSource,
                changedSource);
            var capture = new UnityGameViewScreenshotCapture(
                presentationAdapter,
                new UnityGameViewResolutionAdapter(new SuccessfulOrphanCleaner()),
                new CountingEditorUpdateAwaiter());

            var result = capture.CaptureAsync(
                    new IpcScreenshotCaptureRequest(
                        Guid.Parse("62485cbb-c8b1-4e2c-b5fc-80b47c8d410c"),
                        IpcScreenshotTarget.Game,
                        RequestedWidth: null,
                        RequestedHeight: null),
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(ScreenshotErrorCodes.ScreenshotCaptureUnsupported));
            Assert.That(
                result.ErrorMessage,
                Is.EqualTo("GameView presentation mapping changed during its immediate repaint."));
            Assert.That(presentationAdapter.ImmediateRepaintCount, Is.EqualTo(1));
        }

        private GameViewPresentationSource CreateSource (
            int width,
            int height,
            float backingScale)
        {
            var colorSpace = UnityScreenshotPixelNormalizer.ResolveColorSpace();
            var graphicsFormat = colorSpace == IpcScreenshotColorSpace.Linear
                ? GraphicsFormat.R8G8B8A8_SRGB
                : GraphicsFormat.R8G8B8A8_UNorm;
            Assert.That(
                UnityScreenshotPixelNormalizer.TryCreateRenderTexture(
                    width,
                    height,
                    "uCLI GameView capture freshness test",
                    graphicsFormat,
                    out sourceTexture,
                    out var creationError),
                Is.True,
                creationError);

            gameView = ScriptableObject.CreateInstance<FakeGameView>();
            return new GameViewPresentationSource(
                gameView,
                sourceTexture,
                width,
                height,
                backingScale,
                TargetDisplay: 0,
                TargetInView: new Rect(0f, 0f, width, height),
                DeviceFlippedTargetInView: new Rect(0f, 0f, width, height),
                SourceUvTransform: new Vector4(1f, 1f, 0f, 0f));
        }

        private sealed class FailingRepaintPresentationAdapter : IGameViewPresentationAdapter
        {
            private readonly GameViewPresentationSource source;

            public FailingRepaintPresentationAdapter (GameViewPresentationSource source)
            {
                this.source = source;
            }

            public int ImmediateRepaintCount { get; private set; }

            public bool TryGetSource (
                out GameViewPresentationSource current,
                out string errorMessage)
            {
                current = source;
                errorMessage = null;
                return true;
            }

            public bool TryValidateSource (
                GameViewPresentationSource candidate,
                out string errorMessage)
            {
                var isCurrent = ReferenceEquals(candidate, source);
                errorMessage = isCurrent ? null : "Presentation source changed.";
                return isCurrent;
            }

            public bool IsCurrentTarget (EditorWindow candidate)
            {
                return candidate == source.View;
            }

            public bool TryRepaintImmediately (
                EditorWindow candidate,
                out string errorMessage)
            {
                if (candidate != source.View)
                {
                    errorMessage = "Presentation target changed.";
                    return false;
                }

                ImmediateRepaintCount++;
                errorMessage = "Fresh presentation unavailable.";
                return false;
            }
        }

        private sealed class MappingChangingPresentationAdapter : IGameViewPresentationAdapter
        {
            private readonly GameViewPresentationSource initialSource;

            private readonly GameViewPresentationSource changedSource;

            private int sourceReadCount;

            public MappingChangingPresentationAdapter (
                GameViewPresentationSource initialSource,
                GameViewPresentationSource changedSource)
            {
                this.initialSource = initialSource;
                this.changedSource = changedSource;
            }

            public int ImmediateRepaintCount { get; private set; }

            public bool TryGetSource (
                out GameViewPresentationSource source,
                out string errorMessage)
            {
                sourceReadCount++;
                source = sourceReadCount >= 3 ? changedSource : initialSource;
                errorMessage = null;
                return true;
            }

            public bool TryValidateSource (
                GameViewPresentationSource source,
                out string errorMessage)
            {
                errorMessage = null;
                return true;
            }

            public bool IsCurrentTarget (EditorWindow candidate)
            {
                return candidate == initialSource.View;
            }

            public bool TryRepaintImmediately (
                EditorWindow candidate,
                out string errorMessage)
            {
                if (candidate != initialSource.View)
                {
                    errorMessage = "Presentation target changed.";
                    return false;
                }

                ImmediateRepaintCount++;
                errorMessage = null;
                return true;
            }
        }

        private sealed class CountingEditorUpdateAwaiter : IUnityEditorUpdateAwaiter
        {
            public int WaitCount { get; private set; }

            public Task WaitForNextUpdateAsync (CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                WaitCount++;
                return Task.CompletedTask;
            }
        }

        private sealed class SuccessfulOrphanCleaner : IUnityScreenshotResolutionOrphanCleaner
        {
            public bool TryCleanup (out string errorMessage)
            {
                errorMessage = null;
                return true;
            }
        }

        private sealed class FakeGameView : EditorWindow
        {
        }
    }
}
