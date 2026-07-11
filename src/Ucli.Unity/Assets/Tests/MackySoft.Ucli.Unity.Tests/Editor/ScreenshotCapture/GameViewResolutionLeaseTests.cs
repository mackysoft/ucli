using System.Collections.Generic;
using MackySoft.Ucli.Unity.ScreenshotCapture;
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
            UnityScreenshotResolutionLeaseRegistry.ClearForTests();
        }

        [TearDown]
        public void TearDown ()
        {
            UnityScreenshotResolutionLeaseRegistry.ClearForTests();
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
                var result = lease.TryRestore(out var errorMessage);

                Assert.That(result, Is.False);
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
                var result = lease.TryRestore(out _);

                Assert.That(result, Is.False);
                Assert.That(gameView.SelectionCallbackCallCount, Is.Zero);
                Assert.That(gameView.selectedSizeIndex, Is.EqualTo(1));
                Assert.That(group.Sizes, Is.EqualTo(new[] { originalSize, temporarySize, unrelatedSize }));

                group.Sizes.Remove(unrelatedSize);
                Assert.That(lease.TryRestore(out var cleanupError), Is.True, cleanupError);
            }
            finally
            {
                Object.DestroyImmediate(gameView);
            }
        }

        private static UnityEditorScreenshotReflectionAdapter.GameViewResolutionLease CreateLease (
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
            return new UnityEditorScreenshotReflectionAdapter.GameViewResolutionLease(
                gameView,
                sizes,
                typeof(FakeSizes).GetProperty(nameof(FakeSizes.currentGroup)),
                typeof(FakeSizes).GetProperty(nameof(FakeSizes.currentGroupType)),
                group,
                "Standalone",
                temporarySize,
                label,
                temporaryIndex,
                originalIndex,
                originalCount,
                originalSize,
                typeof(FakeGameView).GetProperty(nameof(FakeGameView.selectedSizeIndex)),
                typeof(FakeGameView).GetMethod(nameof(FakeGameView.SizeSelectionCallback)),
                typeof(FakeSizeGroup).GetMethod(nameof(FakeSizeGroup.GetTotalCount)),
                typeof(FakeSizeGroup).GetMethod(nameof(FakeSizeGroup.GetGameViewSize)),
                typeof(FakeSizeGroup).GetMethod(nameof(FakeSizeGroup.RemoveCustomSize)));
        }

        public sealed class FakeGameView : EditorWindow
        {
            public int selectedSizeIndex { get; private set; }

            public int SelectionCallbackCallCount { get; private set; }

            public void SetSelectedSizeIndex (int index)
            {
                selectedSizeIndex = index;
            }

            public void SizeSelectionCallback (int index, object _)
            {
                SelectionCallbackCallCount++;
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
    }
}
