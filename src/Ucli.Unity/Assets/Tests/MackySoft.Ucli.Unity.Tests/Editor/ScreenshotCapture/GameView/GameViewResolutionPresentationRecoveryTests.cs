using MackySoft.Ucli.Unity.ScreenshotCapture.GameView;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class GameViewResolutionPresentationRecoveryTests
    {
        [SetUp]
        public void SetUp ()
        {
            GameViewResolutionPresentationRecovery.ClearForTests();
        }

        [TearDown]
        public void TearDown ()
        {
            GameViewResolutionPresentationRecovery.ClearForTests();
        }

        [Test]
        [Category("Size.Small")]
        public void RetryDeferredRecovery_WhenPresentationConvergesAfterDelay_ReleasesOwnership ()
        {
            var gameView = EditorWindow.CreateInstance<FakeGameView>();
            var originalTexture = new RenderTexture(100, 50, 0);
            var requestedTexture = new RenderTexture(321, 197, 0);
            var originalSource = Source(gameView, originalTexture, 100, 50);
            var adapter = new FakePresentationAdapter
            {
                CurrentSource = Source(gameView, requestedTexture, 321, 197),
            };
            var recovery = new GameViewResolutionPresentationRecovery(originalSource, adapter);

            try
            {
                Assert.That(recovery.TrySchedule(out var scheduleError), Is.True, scheduleError);

                AdvanceDeferredRecovery(recovery, 300);

                Assert.That(recovery.IsScheduled, Is.True);
                Assert.That(GameViewResolutionPresentationRecovery.HasPending(gameView), Is.True);

                adapter.CurrentSource = originalSource;
                AdvanceDeferredRecovery(recovery, 100);

                Assert.That(adapter.ImmediateRepaintCount, Is.EqualTo(1));
                Assert.That(recovery.IsScheduled, Is.False);
                Assert.That(GameViewResolutionPresentationRecovery.HasPending(gameView), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(requestedTexture);
                Object.DestroyImmediate(originalTexture);
                Object.DestroyImmediate(gameView);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void TryReserve_BeforeScheduling_BlocksCaptureUntilOwnershipIsReleased ()
        {
            var gameView = EditorWindow.CreateInstance<FakeGameView>();
            var originalTexture = new RenderTexture(100, 50, 0);
            var recovery = new GameViewResolutionPresentationRecovery(
                Source(gameView, originalTexture, 100, 50),
                new FakePresentationAdapter());

            try
            {
                Assert.That(recovery.TryReserve(out var reserveError), Is.True, reserveError);
                Assert.That(recovery.IsScheduled, Is.False);
                Assert.That(GameViewResolutionPresentationRecovery.HasPending(gameView), Is.True);

                recovery.ReleaseOwnership();

                Assert.That(recovery.IsPending, Is.False);
                Assert.That(GameViewResolutionPresentationRecovery.HasPending(gameView), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(originalTexture);
                Object.DestroyImmediate(gameView);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void TryReserve_WhenAnotherRecoveryOwnsTarget_RejectsWithoutReplacingOwner ()
        {
            var gameView = EditorWindow.CreateInstance<FakeGameView>();
            var originalTexture = new RenderTexture(100, 50, 0);
            var originalSource = Source(gameView, originalTexture, 100, 50);
            var first = new GameViewResolutionPresentationRecovery(
                originalSource,
                new FakePresentationAdapter());
            var second = new GameViewResolutionPresentationRecovery(
                originalSource,
                new FakePresentationAdapter());

            try
            {
                Assert.That(first.TryReserve(out var firstError), Is.True, firstError);

                Assert.That(second.TryReserve(out var secondError), Is.False);
                Assert.That(secondError, Does.Contain("Another recovery"));
                Assert.That(GameViewResolutionPresentationRecovery.HasPending(gameView), Is.True);

                first.ReleaseOwnership();
                Assert.That(second.TryReserve(out secondError), Is.True, secondError);
            }
            finally
            {
                first.ReleaseOwnership();
                second.ReleaseOwnership();
                Object.DestroyImmediate(originalTexture);
                Object.DestroyImmediate(gameView);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void RetryDeferredRecovery_WhileTargetLivesAndDimensionsDoNotConverge_DoesNotGiveUp ()
        {
            var gameView = EditorWindow.CreateInstance<FakeGameView>();
            var originalTexture = new RenderTexture(100, 50, 0);
            var requestedTexture = new RenderTexture(321, 197, 0);
            var recovery = new GameViewResolutionPresentationRecovery(
                Source(gameView, originalTexture, 100, 50),
                new FakePresentationAdapter
                {
                    CurrentSource = Source(gameView, requestedTexture, 321, 197),
                });

            try
            {
                Assert.That(recovery.TrySchedule(out var scheduleError), Is.True, scheduleError);

                AdvanceDeferredRecovery(recovery, 1000);

                Assert.That(recovery.IsScheduled, Is.True);
                Assert.That(GameViewResolutionPresentationRecovery.HasPending(gameView), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(requestedTexture);
                Object.DestroyImmediate(originalTexture);
                Object.DestroyImmediate(gameView);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void RetryDeferredRecovery_WhenTargetIsDestroyed_ReleasesSubscription ()
        {
            var gameView = EditorWindow.CreateInstance<FakeGameView>();
            var originalTexture = new RenderTexture(100, 50, 0);
            var recovery = new GameViewResolutionPresentationRecovery(
                Source(gameView, originalTexture, 100, 50),
                new FakePresentationAdapter { CurrentSource = null });

            try
            {
                Assert.That(recovery.TrySchedule(out var scheduleError), Is.True, scheduleError);
                Object.DestroyImmediate(gameView);

                recovery.RetryDeferredRecovery();

                Assert.That(recovery.IsScheduled, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(originalTexture);
                if (gameView != null)
                {
                    Object.DestroyImmediate(gameView);
                }
            }
        }

        [Test]
        [Category("Size.Small")]
        public void RetryDeferredRecovery_WhenTargetIsDestroyedDuringCountdown_ReleasesImmediately ()
        {
            var gameView = EditorWindow.CreateInstance<FakeGameView>();
            var originalTexture = new RenderTexture(100, 50, 0);
            var adapter = new FakePresentationAdapter { CurrentSource = null };
            var recovery = new GameViewResolutionPresentationRecovery(
                Source(gameView, originalTexture, 100, 50),
                adapter);

            try
            {
                Assert.That(recovery.TrySchedule(out var scheduleError), Is.True, scheduleError);
                recovery.RetryDeferredRecovery();
                Assert.That(recovery.IsScheduled, Is.True);

                Object.DestroyImmediate(gameView);
                recovery.RetryDeferredRecovery();

                Assert.That(recovery.IsScheduled, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(originalTexture);
                if (gameView != null)
                {
                    Object.DestroyImmediate(gameView);
                }
            }
        }

        [Test]
        [Category("Size.Small")]
        public void RetryDeferredRecovery_WhenTargetIsNoLongerCurrent_ReleasesOwnership ()
        {
            var gameView = EditorWindow.CreateInstance<FakeGameView>();
            var originalTexture = new RenderTexture(100, 50, 0);
            var adapter = new FakePresentationAdapter
            {
                CurrentSource = null,
                TargetIsCurrent = false,
            };
            var recovery = new GameViewResolutionPresentationRecovery(
                Source(gameView, originalTexture, 100, 50),
                adapter);

            try
            {
                Assert.That(recovery.TrySchedule(out var scheduleError), Is.True, scheduleError);

                recovery.RetryDeferredRecovery();

                Assert.That(recovery.IsScheduled, Is.False);
                Assert.That(GameViewResolutionPresentationRecovery.HasPending(gameView), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(originalTexture);
                Object.DestroyImmediate(gameView);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void RetryDeferredRecovery_WhenOnlyExternalMappingChanged_CompletesOwnedDimensionRecovery ()
        {
            var gameView = EditorWindow.CreateInstance<FakeGameView>();
            var originalTexture = new RenderTexture(100, 50, 0);
            var originalSource = Source(gameView, originalTexture, 100, 50);
            var changedMappingSource = new GameViewPresentationSource(
                gameView,
                originalTexture,
                Width: 100,
                Height: 50,
                BackingScale: 2f,
                TargetDisplay: 1,
                TargetInView: new Rect(2f, 3f, 90f, 40f),
                DeviceFlippedTargetInView: new Rect(2f, 7f, 90f, 40f),
                SourceUvTransform: new Vector4(1f, -1f, 0f, 1f));
            var adapter = new FakePresentationAdapter { CurrentSource = changedMappingSource };
            var recovery = new GameViewResolutionPresentationRecovery(originalSource, adapter);

            try
            {
                Assert.That(recovery.TrySchedule(out var scheduleError), Is.True, scheduleError);

                AdvanceDeferredRecovery(recovery, 100);

                Assert.That(recovery.IsScheduled, Is.False);
                Assert.That(adapter.CurrentSource, Is.SameAs(changedMappingSource));
            }
            finally
            {
                Object.DestroyImmediate(originalTexture);
                Object.DestroyImmediate(gameView);
            }
        }

        private static void AdvanceDeferredRecovery (
            GameViewResolutionPresentationRecovery recovery,
            int updateCount)
        {
            for (var update = 0; update < updateCount; update++)
            {
                recovery.RetryDeferredRecovery();
            }
        }

        private static GameViewPresentationSource Source (
            EditorWindow gameView,
            RenderTexture renderTexture,
            int width,
            int height)
        {
            return new GameViewPresentationSource(
                gameView,
                renderTexture,
                width,
                height,
                BackingScale: 1f,
                TargetDisplay: 0,
                TargetInView: new Rect(0f, 0f, width, height),
                DeviceFlippedTargetInView: new Rect(0f, 0f, width, height),
                SourceUvTransform: new Vector4(1f, 1f, 0f, 0f));
        }

        private sealed class FakePresentationAdapter : IGameViewPresentationAdapter
        {
            public GameViewPresentationSource CurrentSource { get; set; }

            public bool TargetIsCurrent { get; set; } = true;

            public int ImmediateRepaintCount { get; private set; }

            public bool TryGetSource (
                out GameViewPresentationSource source,
                out string errorMessage)
            {
                source = CurrentSource;
                errorMessage = source == null ? "Presentation source is unavailable." : null;
                return source != null;
            }

            public bool IsCurrentTarget (EditorWindow gameView)
            {
                return TargetIsCurrent;
            }

            public bool TryRepaintImmediately (
                EditorWindow gameView,
                out string errorMessage)
            {
                ImmediateRepaintCount++;
                errorMessage = null;
                return true;
            }
        }

        private sealed class FakeGameView : EditorWindow
        {
        }
    }
}
