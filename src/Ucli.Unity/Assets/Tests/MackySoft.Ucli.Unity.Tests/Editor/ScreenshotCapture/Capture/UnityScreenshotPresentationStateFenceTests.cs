using MackySoft.Ucli.Unity.ScreenshotCapture.Capture;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityScreenshotPresentationStateFenceTests
    {
        [Test]
        [Category("Size.Small")]
        public void TryValidateStable_WithSameStableState_ReturnsTrue ()
        {
            var pipeline = new StubRenderPipeline();
            var before = State(pipeline: pipeline);
            var after = State(pipeline: pipeline);

            var result = UnityScreenshotPresentationStateFence.TryValidateStable(
                before,
                after,
                out var errorMessage);

            Assert.That(result, Is.True, errorMessage);
        }

        [Test]
        [Category("Size.Small")]
        public void TryValidateStable_WhenPauseChanges_ReturnsFalse ()
        {
            var result = UnityScreenshotPresentationStateFence.TryValidateStable(
                State(isPaused: false),
                State(isPaused: true),
                out _);

            Assert.That(result, Is.False);
        }

        [Test]
        [Category("Size.Small")]
        public void TryValidateStable_WhenQualityChanges_ReturnsFalse ()
        {
            var result = UnityScreenshotPresentationStateFence.TryValidateStable(
                State(qualityLevel: 1),
                State(qualityLevel: 2),
                out _);

            Assert.That(result, Is.False);
        }

        [Test]
        [Category("Size.Small")]
        public void TryValidateStable_WhenCurrentPipelineIdentityChanges_ReturnsFalse ()
        {
            var result = UnityScreenshotPresentationStateFence.TryValidateStable(
                State(pipeline: new StubRenderPipeline()),
                State(pipeline: new StubRenderPipeline()),
                out _);

            Assert.That(result, Is.False);
        }

        [Test]
        [Category("Size.Small")]
        public void TryValidateStable_WhenPipelineAssetIdentityChanges_ReturnsFalse ()
        {
            var pipelineAsset = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(
                "Assets/Settings/URP-HighFidelity.asset");
            Assert.That(pipelineAsset, Is.Not.Null);

            var result = UnityScreenshotPresentationStateFence.TryValidateStable(
                State(pipelineAsset: pipelineAsset),
                State(pipelineAsset: null),
                out _);

            Assert.That(result, Is.False);
        }

        [Test]
        [Category("Size.Small")]
        public void TryValidateStable_WhenPipelineAssetIsDestroyedAfterFirstObservation_ReturnsFalse ()
        {
            var pipelineAsset = ScriptableObject.CreateInstance<StubRenderPipelineAsset>();
            var before = State(pipelineAsset: pipelineAsset);
            Object.DestroyImmediate(pipelineAsset);
            var after = State(pipelineAsset: pipelineAsset);

            var result = UnityScreenshotPresentationStateFence.TryValidateStable(
                before,
                after,
                out _);

            Assert.That(result, Is.False);
        }

        [TestCase(true, true)]
        [TestCase(false, false)]
        [Category("Size.Small")]
        public void TryValidateObservation_DuringTransition_ReturnsFalse (
            bool shaderCompiling,
            bool pipelineSwitchCompleted)
        {
            var result = UnityScreenshotPresentationStateFence.TryValidateObservation(
                State(
                    shaderCompiling: shaderCompiling,
                    pipelineSwitchCompleted: pipelineSwitchCompleted),
                out _);

            Assert.That(result, Is.False);
        }

        private static UnityScreenshotPresentationStateFence.PresentationState State (
            bool isPaused = false,
            bool shaderCompiling = false,
            bool pipelineSwitchCompleted = true,
            int qualityLevel = 1,
            RenderPipelineAsset pipelineAsset = null,
            RenderPipeline pipeline = null)
        {
            return new UnityScreenshotPresentationStateFence.PresentationState(
                isPaused,
                shaderCompiling,
                pipelineSwitchCompleted,
                qualityLevel,
                pipelineAsset != null ? pipelineAsset.GetInstanceID() : (int?)null,
                pipeline);
        }

        private sealed class StubRenderPipeline : RenderPipeline
        {
#if UNITY_6000_1_OR_NEWER
            [System.Obsolete]
#endif
            protected override void Render (
                ScriptableRenderContext context,
                Camera[] cameras)
            {
            }
        }

        private sealed class StubRenderPipelineAsset : RenderPipelineAsset
        {
            protected override RenderPipeline CreatePipeline ()
            {
                return new StubRenderPipeline();
            }
        }
    }
}
