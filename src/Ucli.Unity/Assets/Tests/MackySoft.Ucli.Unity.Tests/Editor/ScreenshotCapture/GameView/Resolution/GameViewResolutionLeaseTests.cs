using System.Collections.Generic;
using MackySoft.Ucli.Unity.ScreenshotCapture.GameView;
using MackySoft.Ucli.Unity.ScreenshotCapture.GameView.Resolution;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class GameViewResolutionLeaseTests
    {
        [SetUp]
        public void SetUp ()
        {
            GameViewResolutionLease.ClearActiveOwnershipForTests();
            GameViewResolutionPresentationRecovery.ClearForTests();
            UnityScreenshotResolutionLeaseRegistry.ClearForTests();
        }

        [TearDown]
        public void TearDown ()
        {
            GameViewResolutionLease.ClearActiveOwnershipForTests();
            GameViewResolutionPresentationRecovery.ClearForTests();
            UnityScreenshotResolutionLeaseRegistry.ClearForTests();
        }

        [Test]
        [Category("Size.Small")]
        public void TryRestore_WithRequestOwnedSelection_RestoresExactState ()
        {
            var originalSize = new object();
            var temporarySize = new object();
            var group = new FakeSizeGroup(originalSize, temporarySize);
            var gameView = EditorWindow.CreateInstance<FakeGameView>();
            gameView.SetSelectedSizeIndex(1);
            var lease = CreateLease(
                gameView,
                group,
                temporarySize,
                temporaryIndex: 1,
                originalIndex: 0,
                originalCount: 1,
                originalSize);

            try
            {
                var result = lease.TryRestore(
                    AcceptPresentationRecoveryOwnership,
                    out var errorMessage);

                Assert.That(
                    result,
                    Is.EqualTo(GameViewResolutionLease.RestoreOutcome.RestoredOriginal),
                    errorMessage);
                Assert.That(gameView.selectedSizeIndex, Is.Zero);
                Assert.That(gameView.SelectionCallbackCallCount, Is.EqualTo(1));
                Assert.That(group.Sizes, Is.EqualTo(new[] { originalSize }));
                Assert.That(lease.CanRetryRestore, Is.False);
                Assert.That(
                    UnityScreenshotResolutionLeaseRegistry.TryRead(out var markers, out var registryError),
                    Is.True,
                    registryError);
                Assert.That(markers, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(gameView);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void TryRestore_WhenPresentationOwnershipIsRejected_RetainsMarkerUntilRetry ()
        {
            var originalSize = new object();
            var temporarySize = new object();
            var group = new FakeSizeGroup(originalSize, temporarySize);
            var gameView = EditorWindow.CreateInstance<FakeGameView>();
            gameView.SetSelectedSizeIndex(1);
            var lease = CreateLease(
                gameView,
                group,
                temporarySize,
                temporaryIndex: 1,
                originalIndex: 0,
                originalCount: 1,
                originalSize);

            try
            {
                var outcome = lease.TryRestore(
                    RejectPresentationRecoveryOwnership,
                    out var errorMessage);

                Assert.That(
                    outcome,
                    Is.EqualTo(GameViewResolutionLease.RestoreOutcome.Retryable));
                Assert.That(errorMessage, Does.Contain("ownership could not be retained"));
                Assert.That(gameView.selectedSizeIndex, Is.Zero);
                Assert.That(group.Sizes, Is.EqualTo(new[] { originalSize }));
                Assert.That(lease.CanRetryRestore, Is.True);
                Assert.That(
                    UnityScreenshotResolutionLeaseRegistry.TryRead(
                        out var markers,
                        out var registryError),
                    Is.True,
                    registryError);
                Assert.That(markers, Has.Count.EqualTo(1));

                outcome = lease.TryRestore(
                    AcceptPresentationRecoveryOwnership,
                    out errorMessage);

                Assert.That(
                    outcome,
                    Is.EqualTo(GameViewResolutionLease.RestoreOutcome.RestoredOriginal),
                    errorMessage);
                Assert.That(
                    UnityScreenshotResolutionLeaseRegistry.TryRead(
                        out markers,
                        out registryError),
                    Is.True,
                    registryError);
                Assert.That(markers, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(gameView);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void TryRestore_WhenPresentationOwnershipThrows_RetainsMarkerUntilRetry ()
        {
            var originalSize = new object();
            var temporarySize = new object();
            var group = new FakeSizeGroup(originalSize, temporarySize);
            var gameView = EditorWindow.CreateInstance<FakeGameView>();
            gameView.SetSelectedSizeIndex(1);
            var lease = CreateLease(
                gameView,
                group,
                temporarySize,
                temporaryIndex: 1,
                originalIndex: 0,
                originalCount: 1,
                originalSize);

            try
            {
                var outcome = lease.TryRestore(
                    ThrowPresentationRecoveryOwnership,
                    out var errorMessage);

                Assert.That(
                    outcome,
                    Is.EqualTo(GameViewResolutionLease.RestoreOutcome.Retryable));
                Assert.That(errorMessage, Does.Contain("Expected ownership failure"));
                Assert.That(lease.CanRetryRestore, Is.True);
                Assert.That(
                    UnityScreenshotResolutionLeaseRegistry.TryRead(
                        out var markers,
                        out var registryError),
                    Is.True,
                    registryError);
                Assert.That(markers, Has.Count.EqualTo(1));

                Assert.That(
                    lease.TryRestore(
                        AcceptPresentationRecoveryOwnership,
                        out var cleanupError),
                    Is.EqualTo(GameViewResolutionLease.RestoreOutcome.RestoredOriginal),
                    cleanupError);
            }
            finally
            {
                Object.DestroyImmediate(gameView);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void TryValidateRestoredState_WhenAnotherExactGameViewAppears_RemainsValid ()
        {
            var originalSize = new object();
            var temporarySize = new object();
            var group = new FakeSizeGroup(originalSize, temporarySize);
            var gameView = EditorWindow.CreateInstance<FakeGameView>();
            FakeGameView otherGameView = null;
            gameView.SetSelectedSizeIndex(1);
            var lease = CreateLease(
                gameView,
                group,
                temporarySize,
                temporaryIndex: 1,
                originalIndex: 0,
                originalCount: 1,
                originalSize);

            try
            {
                Assert.That(
                    lease.TryRestore(
                        AcceptPresentationRecoveryOwnership,
                        out var restoreError),
                    Is.EqualTo(GameViewResolutionLease.RestoreOutcome.RestoredOriginal),
                    restoreError);
                otherGameView = EditorWindow.CreateInstance<FakeGameView>();

                Assert.That(
                    lease.TryValidateRestoredState(out var validationError),
                    Is.True,
                    validationError);
                Assert.That(lease.IsOriginalPresentationRecoveryApplicable(), Is.True);
            }
            finally
            {
                if (otherGameView != null)
                {
                    Object.DestroyImmediate(otherGameView);
                }

                Object.DestroyImmediate(gameView);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void IsOriginalPresentationRecoveryApplicable_WhenOnlyUnrelatedEntryAppears_RemainsTrue ()
        {
            var originalSize = new object();
            var temporarySize = new object();
            var unrelatedSize = new object();
            var group = new FakeSizeGroup(originalSize, temporarySize);
            var gameView = EditorWindow.CreateInstance<FakeGameView>();
            gameView.SetSelectedSizeIndex(1);
            var lease = CreateLease(
                gameView,
                group,
                temporarySize,
                temporaryIndex: 1,
                originalIndex: 0,
                originalCount: 1,
                originalSize);

            try
            {
                Assert.That(
                    lease.TryRestore(
                        AcceptPresentationRecoveryOwnership,
                        out var restoreError),
                    Is.EqualTo(GameViewResolutionLease.RestoreOutcome.RestoredOriginal),
                    restoreError);
                group.Sizes.Add(unrelatedSize);

                Assert.That(lease.TryValidateRestoredState(out _), Is.False);
                Assert.That(lease.IsOriginalPresentationRecoveryApplicable(), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(gameView);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void IsOriginalPresentationRecoveryApplicable_WhenUserSelectionChanges_ReturnsFalse ()
        {
            var originalSize = new object();
            var userSize = new object();
            var temporarySize = new object();
            var group = new FakeSizeGroup(originalSize, userSize, temporarySize);
            var gameView = EditorWindow.CreateInstance<FakeGameView>();
            gameView.SetSelectedSizeIndex(2);
            var lease = CreateLease(
                gameView,
                group,
                temporarySize,
                temporaryIndex: 2,
                originalIndex: 0,
                originalCount: 2,
                originalSize);

            try
            {
                Assert.That(
                    lease.TryRestore(
                        AcceptPresentationRecoveryOwnership,
                        out var restoreError),
                    Is.EqualTo(GameViewResolutionLease.RestoreOutcome.RestoredOriginal),
                    restoreError);
                gameView.SetSelectedSizeIndex(1);

                Assert.That(lease.TryValidateRestoredState(out _), Is.False);
                Assert.That(lease.IsOriginalPresentationRecoveryApplicable(), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(gameView);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void TryRestore_WhenAnotherExactGameViewExists_DoesNotMutateOwnedState ()
        {
            var originalSize = new object();
            var temporarySize = new object();
            var group = new FakeSizeGroup(originalSize, temporarySize);
            var gameView = EditorWindow.CreateInstance<FakeGameView>();
            var otherGameView = EditorWindow.CreateInstance<FakeGameView>();
            gameView.SetSelectedSizeIndex(1);
            otherGameView.SetSelectedSizeIndex(0);
            var lease = CreateLease(
                gameView,
                group,
                temporarySize,
                temporaryIndex: 1,
                originalIndex: 0,
                originalCount: 1,
                originalSize);

            try
            {
                var result = lease.TryRestore(
                    AcceptPresentationRecoveryOwnership,
                    out var errorMessage);

                Assert.That(
                    result,
                    Is.EqualTo(GameViewResolutionLease.RestoreOutcome.Retryable));
                Assert.That(errorMessage, Does.Contain("exactly one"));
                Assert.That(gameView.SelectionCallbackCallCount, Is.Zero);
                Assert.That(gameView.selectedSizeIndex, Is.EqualTo(1));
                Assert.That(group.Sizes, Is.EqualTo(new[] { originalSize, temporarySize }));
                Assert.That(lease.CanRetryRestore, Is.True);
                Assert.That(
                    UnityScreenshotResolutionLeaseRegistry.TryRead(out var markers, out var registryError),
                    Is.True,
                    registryError);
                Assert.That(markers, Has.Count.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(otherGameView);
                lease.TryRestore(AcceptPresentationRecoveryOwnership, out _);
                Object.DestroyImmediate(gameView);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void TryActivateLease_WhenSelectionThrows_ReturnsNoLeaseAndCleansOwnedState ()
        {
            var originalSize = new object();
            var temporarySize = new object();
            var group = new FakeSizeGroup(originalSize, temporarySize);
            var gameView = EditorWindow.CreateInstance<FakeGameView>();
            gameView.SetSelectedSizeIndex(0);
            gameView.ThrowOnNextSelection = true;
            var candidate = CreateLease(
                gameView,
                group,
                temporarySize,
                temporaryIndex: 1,
                originalIndex: 0,
                originalCount: 1,
                originalSize);

            try
            {
                var result = UnityGameViewResolutionAdapter.TryActivateLease(
                    candidate,
                    AcceptPresentationRecoveryOwnership,
                    out var lease,
                    out var errorMessage);

                Assert.That(result, Is.False);
                Assert.That(lease, Is.Null);
                Assert.That(errorMessage, Is.Not.Empty);
                Assert.That(gameView.selectedSizeIndex, Is.Zero);
                Assert.That(group.Sizes, Is.EqualTo(new[] { originalSize }));
                Assert.That(candidate.CanRetryRestore, Is.False);
                Assert.That(
                    UnityScreenshotResolutionLeaseRegistry.TryRead(out var markers, out var registryError),
                    Is.True,
                    registryError);
                Assert.That(markers, Is.Empty);
            }
            finally
            {
                if (candidate.CanRetryRestore)
                {
                    candidate.TryRestore(AcceptPresentationRecoveryOwnership, out _);
                }

                Object.DestroyImmediate(gameView);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void TryActivateLease_WhenImmediateRecoveryIsUnsafe_RetainsInternalRecoveryOwnership ()
        {
            var originalSize = new object();
            var temporarySize = new object();
            var group = new FakeSizeGroup(originalSize, temporarySize);
            var gameView = EditorWindow.CreateInstance<FakeGameView>();
            var otherGameView = EditorWindow.CreateInstance<FakeGameView>();
            gameView.SetSelectedSizeIndex(0);
            otherGameView.SetSelectedSizeIndex(0);
            var candidate = CreateLease(
                gameView,
                group,
                temporarySize,
                temporaryIndex: 1,
                originalIndex: 0,
                originalCount: 1,
                originalSize);

            try
            {
                var result = UnityGameViewResolutionAdapter.TryActivateLease(
                    candidate,
                    AcceptPresentationRecoveryOwnership,
                    out var lease,
                    out var errorMessage);

                Assert.That(result, Is.False);
                Assert.That(lease, Is.Null);
                Assert.That(errorMessage, Does.Contain("deferred recovery"));
                Assert.That(candidate.CanRetryRestore, Is.True);
                Assert.That(candidate.IsDeferredRecoveryScheduled, Is.True);
                Assert.That(gameView.SelectionCallbackCallCount, Is.Zero);
                Assert.That(group.Sizes, Is.EqualTo(new[] { originalSize, temporarySize }));

                for (var update = 0; update < 100; update++)
                {
                    candidate.RetryDeferredRecovery();
                }

                Assert.That(candidate.IsDeferredRecoveryScheduled, Is.True);
                Assert.That(candidate.CanRetryRestore, Is.True);
                Assert.That(group.Sizes, Is.EqualTo(new[] { originalSize, temporarySize }));
            }
            finally
            {
                Object.DestroyImmediate(otherGameView);
                candidate.TryRestore(AcceptPresentationRecoveryOwnership, out _);
                Object.DestroyImmediate(gameView);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void RetryDeferredRecovery_WhenTargetIsDestroyed_HandsOwnershipToOrphanCleanup ()
        {
            var originalSize = new object();
            var temporarySize = new object();
            var group = new FakeSizeGroup(originalSize, temporarySize);
            var gameView = EditorWindow.CreateInstance<FakeGameView>();
            var otherGameView = EditorWindow.CreateInstance<FakeGameView>();
            gameView.SetSelectedSizeIndex(0);
            otherGameView.SetSelectedSizeIndex(0);
            var candidate = CreateLease(
                gameView,
                group,
                temporarySize,
                temporaryIndex: 1,
                originalIndex: 0,
                originalCount: 1,
                originalSize);

            try
            {
                Assert.That(
                    UnityGameViewResolutionAdapter.TryActivateLease(
                        candidate,
                        AcceptPresentationRecoveryOwnership,
                        out var lease,
                        out _),
                    Is.False);
                Assert.That(lease, Is.Null);
                Assert.That(candidate.IsDeferredRecoveryScheduled, Is.True);

                candidate.RetryDeferredRecovery();
                Assert.That(candidate.IsDeferredRecoveryScheduled, Is.True);

                Object.DestroyImmediate(gameView);
                candidate.RetryDeferredRecovery();

                Assert.That(candidate.IsDeferredRecoveryScheduled, Is.False);
                Assert.That(candidate.CanRetryRestore, Is.False);
                Assert.That(group.Sizes, Is.EqualTo(new[] { originalSize, temporarySize }));
                Assert.That(
                    UnityScreenshotResolutionLeaseRegistry.TryRead(out var markers, out var registryError),
                    Is.True,
                    registryError);
                Assert.That(markers, Has.Count.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(otherGameView);
                if (gameView != null)
                {
                    Object.DestroyImmediate(gameView);
                }
            }
        }

        [Test]
        [Category("Size.Small")]
        public void RetryDeferredRecovery_WhenBookkeepingLaterSucceeds_InvokesPresentationTransition ()
        {
            var originalSize = new object();
            var temporarySize = new object();
            var group = new FakeSizeGroup(originalSize, temporarySize);
            var gameView = EditorWindow.CreateInstance<FakeGameView>();
            var otherGameView = EditorWindow.CreateInstance<FakeGameView>();
            gameView.SetSelectedSizeIndex(1);
            otherGameView.SetSelectedSizeIndex(0);
            var lease = CreateLease(
                gameView,
                group,
                temporarySize,
                temporaryIndex: 1,
                originalIndex: 0,
                originalCount: 1,
                originalSize);
            var transitionedToPresentationRecovery = false;
            var markerObservedDuringTransition = false;

            try
            {
                lease.ScheduleDeferredRecovery(
                    (out string ownershipError) =>
                    {
                        transitionedToPresentationRecovery = true;
                        markerObservedDuringTransition =
                            UnityScreenshotResolutionLeaseRegistry.TryRead(
                                out var markers,
                                out ownershipError)
                            && markers.Count == 1;
                        if (!markerObservedDuringTransition)
                        {
                            return false;
                        }

                        ownershipError = null;
                        return true;
                    });
                lease.RetryDeferredRecovery();
                Assert.That(transitionedToPresentationRecovery, Is.False);
                Assert.That(lease.IsDeferredRecoveryScheduled, Is.True);

                Object.DestroyImmediate(otherGameView);
                for (var update = 0; update < 31; update++)
                {
                    lease.RetryDeferredRecovery();
                }

                Assert.That(transitionedToPresentationRecovery, Is.True);
                Assert.That(markerObservedDuringTransition, Is.True);
                Assert.That(lease.IsDeferredRecoveryScheduled, Is.False);
                Assert.That(gameView.selectedSizeIndex, Is.Zero);
                Assert.That(group.Sizes, Is.EqualTo(new[] { originalSize }));
                Assert.That(
                    UnityScreenshotResolutionLeaseRegistry.TryRead(out var markers, out var registryError),
                    Is.True,
                    registryError);
                Assert.That(markers, Is.Empty);
            }
            finally
            {
                if (otherGameView != null)
                {
                    Object.DestroyImmediate(otherGameView);
                }

                Object.DestroyImmediate(gameView);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void RetryDeferredRecovery_WhenPresentationOwnershipInitiallyFails_RetainsMarkerAndRetries ()
        {
            var originalSize = new object();
            var temporarySize = new object();
            var group = new FakeSizeGroup(originalSize, temporarySize);
            var gameView = EditorWindow.CreateInstance<FakeGameView>();
            gameView.SetSelectedSizeIndex(1);
            var lease = CreateLease(
                gameView,
                group,
                temporarySize,
                temporaryIndex: 1,
                originalIndex: 0,
                originalCount: 1,
                originalSize);
            var ownershipAvailable = false;
            var ownershipAttemptCount = 0;

            try
            {
                lease.ScheduleDeferredRecovery(
                    (out string ownershipError) =>
                    {
                        ownershipAttemptCount++;
                        ownershipError = ownershipAvailable
                            ? null
                            : "Expected deferred ownership rejection.";
                        return ownershipAvailable;
                    });

                lease.RetryDeferredRecovery();

                Assert.That(ownershipAttemptCount, Is.EqualTo(1));
                Assert.That(lease.IsDeferredRecoveryScheduled, Is.True);
                Assert.That(lease.CanRetryRestore, Is.True);
                Assert.That(
                    UnityScreenshotResolutionLeaseRegistry.TryRead(
                        out var markers,
                        out var registryError),
                    Is.True,
                    registryError);
                Assert.That(markers, Has.Count.EqualTo(1));

                ownershipAvailable = true;
                for (var update = 0; update < 30; update++)
                {
                    lease.RetryDeferredRecovery();
                }

                Assert.That(ownershipAttemptCount, Is.EqualTo(2));
                Assert.That(lease.IsDeferredRecoveryScheduled, Is.False);
                Assert.That(lease.CanRetryRestore, Is.False);
                Assert.That(
                    UnityScreenshotResolutionLeaseRegistry.TryRead(
                        out markers,
                        out registryError),
                    Is.True,
                    registryError);
                Assert.That(markers, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(gameView);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void TryValidateNoPendingRecovery_WhenOrphanCleanupFails_FailsCurrentCaptureGate ()
        {
            var gameView = EditorWindow.CreateInstance<FakeGameView>();
            var cleaner = new TestOrphanCleaner(clearRegistry: false);
            UnityScreenshotResolutionLeaseRegistry.Register(
                new UnityScreenshotResolutionLeaseRegistry.OwnedResolution(
                    UnityScreenshotResolutionLeaseRegistry.CreateLabel(),
                    Width: 321,
                    Height: 197,
                    GroupType: "Standalone",
                    OriginalIndex: 0));

            try
            {
                var result = UnityGameViewScreenshotCapture.TryValidateNoPendingRecovery(
                    gameView,
                    new UnityGameViewResolutionAdapter(cleaner),
                    out var errorMessage);

                Assert.That(result, Is.False);
                Assert.That(errorMessage, Does.Contain("could not be restored"));
                Assert.That(cleaner.CallCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(gameView);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void TryValidateNoPendingRecovery_WithOrphanMarker_RetriesCleanupAndAllowsCapture ()
        {
            var gameView = EditorWindow.CreateInstance<FakeGameView>();
            var cleaner = new TestOrphanCleaner(clearRegistry: true);
            UnityScreenshotResolutionLeaseRegistry.Register(
                new UnityScreenshotResolutionLeaseRegistry.OwnedResolution(
                    UnityScreenshotResolutionLeaseRegistry.CreateLabel(),
                    Width: 321,
                    Height: 197,
                    GroupType: "Standalone",
                    OriginalIndex: 0));

            try
            {
                var result = UnityGameViewScreenshotCapture.TryValidateNoPendingRecovery(
                    gameView,
                    new UnityGameViewResolutionAdapter(cleaner),
                    out var errorMessage);

                Assert.That(result, Is.True, errorMessage);
                Assert.That(cleaner.CallCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(gameView);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void TryValidateNoPendingRecovery_WithLiveLease_DoesNotInvokeOrphanCleanup ()
        {
            var originalSize = new object();
            var temporarySize = new object();
            var group = new FakeSizeGroup(originalSize, temporarySize);
            var gameView = EditorWindow.CreateInstance<FakeGameView>();
            var cleaner = new TestOrphanCleaner(clearRegistry: true);
            gameView.SetSelectedSizeIndex(1);
            var lease = CreateLease(
                gameView,
                group,
                temporarySize,
                temporaryIndex: 1,
                originalIndex: 0,
                originalCount: 1,
                originalSize);

            try
            {
                var result = UnityGameViewScreenshotCapture.TryValidateNoPendingRecovery(
                    gameView,
                    new UnityGameViewResolutionAdapter(cleaner),
                    out var errorMessage);

                Assert.That(result, Is.False);
                Assert.That(errorMessage, Does.Contain("still being restored"));
                Assert.That(cleaner.CallCount, Is.Zero);
            }
            finally
            {
                lease.TryRestore(AcceptPresentationRecoveryOwnership, out _);
                Object.DestroyImmediate(gameView);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void TryRestore_WhenUserSelectedAnotherResolution_DoesNotOverwriteSelection ()
        {
            var originalSize = new object();
            var userSelectedSize = new object();
            var temporarySize = new object();
            var group = new FakeSizeGroup(originalSize, userSelectedSize, temporarySize);
            var gameView = EditorWindow.CreateInstance<FakeGameView>();
            gameView.SetSelectedSizeIndex(1);
            var lease = CreateLease(
                gameView,
                group,
                temporarySize,
                temporaryIndex: 2,
                originalIndex: 0,
                originalCount: 2,
                originalSize);

            try
            {
                var result = lease.TryRestore(
                    AcceptPresentationRecoveryOwnership,
                    out var errorMessage);

                Assert.That(
                    result,
                    Is.EqualTo(GameViewResolutionLease.RestoreOutcome.UserSelectionPreserved));
                Assert.That(errorMessage, Does.Contain("left untouched"));
                Assert.That(gameView.SelectionCallbackCallCount, Is.Zero);
                Assert.That(gameView.selectedSizeIndex, Is.EqualTo(1));
                Assert.That(group.Sizes, Is.EqualTo(new[] { originalSize, userSelectedSize }));
                Assert.That(lease.CanRetryRestore, Is.False);
                Assert.That(
                    UnityScreenshotResolutionLeaseRegistry.TryRead(out var markers, out var registryError),
                    Is.True,
                    registryError);
                Assert.That(markers, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(gameView);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void TryRestore_WhenCollectionChanged_DoesNotMutateSelectionOrCollection ()
        {
            var originalSize = new object();
            var temporarySize = new object();
            var unrelatedSize = new object();
            var group = new FakeSizeGroup(originalSize, temporarySize);
            var gameView = EditorWindow.CreateInstance<FakeGameView>();
            gameView.SetSelectedSizeIndex(1);
            var lease = CreateLease(
                gameView,
                group,
                temporarySize,
                temporaryIndex: 1,
                originalIndex: 0,
                originalCount: 1,
                originalSize);
            group.Sizes.Add(unrelatedSize);

            try
            {
                var result = lease.TryRestore(
                    AcceptPresentationRecoveryOwnership,
                    out _);

                Assert.That(
                    result,
                    Is.EqualTo(GameViewResolutionLease.RestoreOutcome.Retryable));
                Assert.That(gameView.SelectionCallbackCallCount, Is.Zero);
                Assert.That(gameView.selectedSizeIndex, Is.EqualTo(1));
                Assert.That(group.Sizes, Is.EqualTo(new[] { originalSize, temporarySize, unrelatedSize }));

                group.Sizes.Remove(unrelatedSize);
                Assert.That(
                    lease.TryRestore(
                        AcceptPresentationRecoveryOwnership,
                        out var cleanupError),
                    Is.EqualTo(GameViewResolutionLease.RestoreOutcome.RestoredOriginal),
                    cleanupError);
            }
            finally
            {
                Object.DestroyImmediate(gameView);
            }
        }

        private static GameViewResolutionLease CreateLease (
            FakeGameView gameView,
            FakeSizeGroup group,
            object temporarySize,
            int temporaryIndex,
            int originalIndex,
            int originalCount,
            object originalSize)
        {
            var label = UnityScreenshotResolutionLeaseRegistry.CreateLabel();
            UnityScreenshotResolutionLeaseRegistry.Register(
                new UnityScreenshotResolutionLeaseRegistry.OwnedResolution(
                    label,
                    Width: 321,
                    Height: 197,
                    GroupType: "Standalone",
                    OriginalIndex: originalIndex));
            var sizes = new FakeSizes(group);
            var groupAccess = new UnityGameViewResolutionAdapter.ResolutionGroup(
                sizes,
                typeof(FakeSizes).GetProperty(nameof(FakeSizes.currentGroup)),
                typeof(FakeSizes).GetProperty(nameof(FakeSizes.currentGroupType)),
                group,
                "Standalone",
                typeof(FakeGameView).GetProperty(nameof(FakeGameView.selectedSizeIndex)),
                typeof(FakeGameView).GetMethod(nameof(FakeGameView.SizeSelectionCallback)),
                typeof(FakeSizeGroup).GetMethod(nameof(FakeSizeGroup.GetTotalCount)),
                typeof(FakeSizeGroup).GetMethod(nameof(FakeSizeGroup.GetGameViewSize)),
                typeof(FakeSizeGroup).GetMethod(nameof(FakeSizeGroup.RemoveCustomSize)));
            return new GameViewResolutionLease(
                gameView,
                groupAccess,
                new GameViewResolutionLease.OriginalResolutionState(
                    originalIndex,
                    originalCount,
                    originalSize),
                new GameViewResolutionLease.TemporaryResolutionState(
                    label,
                    temporaryIndex,
                    temporarySize));
        }

        private static bool AcceptPresentationRecoveryOwnership (out string errorMessage)
        {
            errorMessage = null;
            return true;
        }

        private static bool RejectPresentationRecoveryOwnership (out string errorMessage)
        {
            errorMessage = "Expected ownership rejection.";
            return false;
        }

        private static bool ThrowPresentationRecoveryOwnership (out string errorMessage)
        {
            errorMessage = null;
            throw new System.InvalidOperationException("Expected ownership failure.");
        }

        public sealed class FakeGameView : EditorWindow
        {
            public int selectedSizeIndex { get; private set; }

            public int SelectionCallbackCallCount { get; private set; }

            public bool ThrowOnNextSelection { get; set; }

            public void SetSelectedSizeIndex (int index)
            {
                selectedSizeIndex = index;
            }

            public void SizeSelectionCallback (int index, object _)
            {
                SelectionCallbackCallCount++;
                if (ThrowOnNextSelection)
                {
                    ThrowOnNextSelection = false;
                    throw new System.InvalidOperationException("Expected selection failure.");
                }

                selectedSizeIndex = index;
            }
        }

        private sealed class FakeSizes
        {
            public FakeSizes (FakeSizeGroup group)
            {
                currentGroup = group;
            }

            public FakeSizeGroup currentGroup { get; }

            public string currentGroupType => "Standalone";
        }

        private sealed class FakeSizeGroup
        {
            public FakeSizeGroup (params object[] sizes)
            {
                Sizes = new List<object>(sizes);
            }

            public List<object> Sizes { get; }

            public int GetTotalCount ()
            {
                return Sizes.Count;
            }

            public object GetGameViewSize (int index)
            {
                return Sizes[index];
            }

            public void RemoveCustomSize (int index)
            {
                Sizes.RemoveAt(index);
            }
        }

        private sealed class TestOrphanCleaner : IUnityScreenshotResolutionOrphanCleaner
        {
            private readonly bool clearRegistry;

            public TestOrphanCleaner (bool clearRegistry)
            {
                this.clearRegistry = clearRegistry;
            }

            public int CallCount { get; private set; }

            public bool TryCleanup (out string errorMessage)
            {
                CallCount++;
                if (!clearRegistry)
                {
                    errorMessage = "Expected cleanup failure.";
                    return false;
                }

                UnityScreenshotResolutionLeaseRegistry.ClearForTests();
                errorMessage = null;
                return true;
            }
        }
    }
}
