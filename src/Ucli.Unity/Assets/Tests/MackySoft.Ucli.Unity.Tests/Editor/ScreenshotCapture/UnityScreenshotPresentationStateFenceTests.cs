using MackySoft.Ucli.Unity.ScreenshotCapture;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityScreenshotPresentationStateFenceTests
    {
        [Test]
        [Category("Size.Small")]
        public void TryValidateStable_WithSameStableState_ReturnsTrue ()
        {
            var pipelineAsset = new object();
            var pipeline = new object();
            var before = State(pipelineAsset: pipelineAsset, pipeline: pipeline);
            var after = State(pipelineAsset: pipelineAsset, pipeline: pipeline);

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
        public void TryValidateStable_WhenPipelineIdentityChanges_ReturnsFalse ()
        {
            var result = UnityScreenshotPresentationStateFence.TryValidateStable(
                State(pipelineAsset: new object(), pipeline: new object()),
                State(pipelineAsset: new object(), pipeline: new object()),
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
            object pipelineAsset = null,
            object pipeline = null)
        {
            return new UnityScreenshotPresentationStateFence.PresentationState(
                isPaused,
                shaderCompiling,
                pipelineSwitchCompleted,
                qualityLevel,
                pipelineAsset,
                pipeline);
        }
    }
}
