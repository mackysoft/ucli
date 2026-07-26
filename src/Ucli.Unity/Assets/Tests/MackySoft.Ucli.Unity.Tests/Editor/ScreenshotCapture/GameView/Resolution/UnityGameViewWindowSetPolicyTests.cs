using System.Collections.Generic;
using MackySoft.Ucli.Unity.ScreenshotCapture.GameView.Resolution;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityGameViewWindowSetPolicyTests
    {
        [Test]
        [Category("Size.Small")]
        public void TryValidateExclusiveTarget_WithOnlyExpectedGameView_ReturnsTrue ()
        {
            var expectedTarget = EditorWindow.CreateInstance<FakeGameView>();

            try
            {
                var result = UnityGameViewWindowSetPolicy.TryValidateExclusiveTarget(
                    expectedTarget,
                    new[] { expectedTarget },
                    out var errorMessage);

                Assert.That(result, Is.True, errorMessage);
            }
            finally
            {
                Object.DestroyImmediate(expectedTarget);
            }
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        [Category("Size.Small")]
        public void TryValidateExclusiveTarget_WithoutOneExactTarget_ReturnsFalse (
            bool containsExpectedTarget,
            bool containsOtherTarget)
        {
            var expectedTarget = EditorWindow.CreateInstance<FakeGameView>();
            var otherTarget = EditorWindow.CreateInstance<FakeGameView>();

            try
            {
                var liveGameViews = new List<EditorWindow>();
                if (containsExpectedTarget)
                {
                    liveGameViews.Add(expectedTarget);
                }

                if (containsOtherTarget)
                {
                    liveGameViews.Add(otherTarget);
                }

                var result = UnityGameViewWindowSetPolicy.TryValidateExclusiveTarget(
                    expectedTarget,
                    liveGameViews,
                    out var errorMessage);

                Assert.That(result, Is.False);
                Assert.That(errorMessage, Is.Not.Empty);
            }
            finally
            {
                Object.DestroyImmediate(otherTarget);
                Object.DestroyImmediate(expectedTarget);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void TryValidateExclusiveTarget_WhenExpectedTargetWasDestroyed_ReturnsFalse ()
        {
            var expectedTarget = EditorWindow.CreateInstance<FakeGameView>();
            var otherTarget = EditorWindow.CreateInstance<FakeGameView>();
            Object.DestroyImmediate(expectedTarget);

            try
            {
                var result = UnityGameViewWindowSetPolicy.TryValidateExclusiveTarget(
                    expectedTarget,
                    new[] { otherTarget },
                    out var errorMessage);

                Assert.That(result, Is.False);
                Assert.That(errorMessage, Is.Not.Empty);
            }
            finally
            {
                Object.DestroyImmediate(otherTarget);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void TryResolveExclusive_WhenOnlyTargetWasDestroyed_ReturnsFalse ()
        {
            var destroyedTarget = EditorWindow.CreateInstance<FakeGameView>();
            Object.DestroyImmediate(destroyedTarget);

            var result = UnityGameViewWindowSetPolicy.TryResolveExclusive(
                new[] { destroyedTarget },
                out var resolvedTarget,
                out var errorMessage);

            Assert.That(result, Is.False);
            Assert.That(resolvedTarget, Is.Null);
            Assert.That(errorMessage, Is.Not.Empty);
        }

        private sealed class FakeGameView : EditorWindow
        {
        }
    }
}
