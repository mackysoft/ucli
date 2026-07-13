using System.Collections.Generic;
using MackySoft.Ucli.Unity.ScreenshotCapture.GameView.Resolution;
using NUnit.Framework;
using UnityEngine;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityScreenshotRequestedResolutionFreshnessTrackerTests
    {
        private readonly List<RenderTexture> textures = new List<RenderTexture>();

        [TearDown]
        public void TearDown ()
        {
            foreach (var texture in textures)
            {
                if (texture != null)
                {
                    Object.DestroyImmediate(texture);
                }
            }

            textures.Clear();
        }

        [Test]
        [Category("Size.Small")]
        public void Observe_AfterFirstDimensionMatch_RequiresSubsequentEditorUpdate ()
        {
            var texture = CreateTexture();
            var tracker = new UnityScreenshotRequestedResolutionFreshnessTracker(321, 197);

            var firstMatch = Observe(tracker, texture, 321, 197, completedEditorUpdateGeneration: 7);
            var sameGeneration = Observe(tracker, texture, 321, 197, completedEditorUpdateGeneration: 7);
            var nextGeneration = Observe(tracker, texture, 321, 197, completedEditorUpdateGeneration: 8);

            Assert.That(
                firstMatch,
                Is.EqualTo(
                    UnityScreenshotRequestedResolutionFreshnessTracker.Observation.WaitingForSubsequentEditorUpdate));
            Assert.That(
                sameGeneration,
                Is.EqualTo(
                    UnityScreenshotRequestedResolutionFreshnessTracker.Observation.WaitingForSubsequentEditorUpdate));
            Assert.That(
                nextGeneration,
                Is.EqualTo(
                    UnityScreenshotRequestedResolutionFreshnessTracker.Observation.ReadyForImmediateRepaint));
        }

        [Test]
        [Category("Size.Small")]
        public void Observe_WhenTextureIdentityChanges_StartsNewFreshnessBaseline ()
        {
            var firstTexture = CreateTexture();
            var replacementTexture = CreateTexture();
            var tracker = new UnityScreenshotRequestedResolutionFreshnessTracker(321, 197);

            Observe(tracker, firstTexture, 321, 197, completedEditorUpdateGeneration: 7);
            var replacement = Observe(
                tracker,
                replacementTexture,
                321,
                197,
                completedEditorUpdateGeneration: 20);
            var replacementRendered = Observe(
                tracker,
                replacementTexture,
                321,
                197,
                completedEditorUpdateGeneration: 21);

            Assert.That(
                replacement,
                Is.EqualTo(
                    UnityScreenshotRequestedResolutionFreshnessTracker.Observation.WaitingForSubsequentEditorUpdate));
            Assert.That(
                replacementRendered,
                Is.EqualTo(
                    UnityScreenshotRequestedResolutionFreshnessTracker.Observation.ReadyForImmediateRepaint));
        }

        [Test]
        [Category("Size.Small")]
        public void Observe_WhenDimensionsDrift_RequiresNewMatchingBaseline ()
        {
            var texture = CreateTexture();
            var tracker = new UnityScreenshotRequestedResolutionFreshnessTracker(321, 197);

            Observe(tracker, texture, 321, 197, completedEditorUpdateGeneration: 7);
            var drifted = Observe(tracker, texture, 320, 197, completedEditorUpdateGeneration: 8);
            var returned = Observe(tracker, texture, 321, 197, completedEditorUpdateGeneration: 9);

            Assert.That(
                drifted,
                Is.EqualTo(
                    UnityScreenshotRequestedResolutionFreshnessTracker.Observation.WaitingForRequestedDimensions));
            Assert.That(
                returned,
                Is.EqualTo(
                    UnityScreenshotRequestedResolutionFreshnessTracker.Observation.WaitingForSubsequentEditorUpdate));
        }

        [Test]
        [Category("Size.Small")]
        public void Observe_WhenTextureDimensionsRemainOld_DoesNotStartFreshnessBaseline ()
        {
            var texture = CreateTexture();
            var tracker = new UnityScreenshotRequestedResolutionFreshnessTracker(321, 197);

            var oldTextureSize = tracker.Observe(
                texture,
                width: 321,
                height: 197,
                textureWidth: 1023,
                textureHeight: 998,
                completedEditorUpdateGeneration: 8);
            var firstExactMatch = Observe(
                tracker,
                texture,
                321,
                197,
                completedEditorUpdateGeneration: 9);

            Assert.That(
                oldTextureSize,
                Is.EqualTo(
                    UnityScreenshotRequestedResolutionFreshnessTracker.Observation.WaitingForRequestedDimensions));
            Assert.That(
                firstExactMatch,
                Is.EqualTo(
                    UnityScreenshotRequestedResolutionFreshnessTracker.Observation.WaitingForSubsequentEditorUpdate));
        }

        [Test]
        [Category("Size.Small")]
        public void Observe_WhenEditorUpdateGenerationWraps_StillRecognizesNewGeneration ()
        {
            var texture = CreateTexture();
            var tracker = new UnityScreenshotRequestedResolutionFreshnessTracker(321, 197);

            Observe(tracker, texture, 321, 197, uint.MaxValue);
            var result = Observe(tracker, texture, 321, 197, completedEditorUpdateGeneration: 0);

            Assert.That(
                result,
                Is.EqualTo(
                    UnityScreenshotRequestedResolutionFreshnessTracker.Observation.ReadyForImmediateRepaint));
        }

        private static UnityScreenshotRequestedResolutionFreshnessTracker.Observation Observe (
            UnityScreenshotRequestedResolutionFreshnessTracker tracker,
            RenderTexture texture,
            int width,
            int height,
            uint completedEditorUpdateGeneration)
        {
            return tracker.Observe(
                texture,
                width,
                height,
                textureWidth: width,
                textureHeight: height,
                completedEditorUpdateGeneration);
        }

        private RenderTexture CreateTexture ()
        {
            var texture = new RenderTexture(width: 1, height: 1, depth: 0);
            textures.Add(texture);
            return texture;
        }
    }
}
