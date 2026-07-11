using MackySoft.Ucli.Unity.ScreenshotCapture.GameView.Resolution;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityGameViewWindowSetPolicyTests
    {
        [Test]
        [Category("Size.Small")]
        public void TryValidateExclusiveTarget_WithOnlyExpectedGameView_ReturnsTrue ()
        {
            var result = UnityGameViewWindowSetPolicy.TryValidateExclusiveTarget(
                expectedTargetInstanceId: 41,
                new[] { 41 },
                out var errorMessage);

            Assert.That(result, Is.True, errorMessage);
        }

        [TestCase(new int[0])]
        [TestCase(new int[] { 41, 42 })]
        [TestCase(new int[] { 42 })]
        [Category("Size.Small")]
        public void TryValidateExclusiveTarget_WithoutOneExactTarget_ReturnsFalse (
            int[] liveGameViewInstanceIds)
        {
            var result = UnityGameViewWindowSetPolicy.TryValidateExclusiveTarget(
                expectedTargetInstanceId: 41,
                liveGameViewInstanceIds,
                out var errorMessage);

            Assert.That(result, Is.False);
            Assert.That(errorMessage, Is.Not.Empty);
        }
    }
}
