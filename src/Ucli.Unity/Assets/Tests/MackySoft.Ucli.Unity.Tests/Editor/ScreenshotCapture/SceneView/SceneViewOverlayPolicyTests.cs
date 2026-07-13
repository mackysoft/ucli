using System;
using MackySoft.Ucli.Unity.ScreenshotCapture.SceneView;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class SceneViewOverlayPolicyTests
    {
        [Test]
        [Category("Size.Small")]
        public void TryEvaluate_WhenMenuCapabilityIsUnavailable_FailsClosed ()
        {
            var result = SceneViewOverlayPolicy.TryEvaluate(
                new SceneViewOverlayPolicy.ObservationSet(
                    SceneViewOverlayPolicy.PresenceObservation.Unavailable,
                    SceneViewOverlayPolicy.PresenceObservation.NotDisplayed,
                    popupContainsOverlayMenu: false,
                    SceneViewOverlayPolicy.PresenceObservation.NotDisplayed,
                    SceneViewOverlayPolicy.ExcludedPresentation.None),
                out var presentation,
                out var errorMessage);

            Assert.That(result, Is.False);
            Assert.That(presentation, Is.EqualTo(SceneViewOverlayPolicy.ExcludedPresentation.None));
            Assert.That(errorMessage, Does.Contain("Overlay Menu visibility capability"));
        }

        [Test]
        [Category("Size.Small")]
        public void TryEvaluate_WhenPopupCapabilityIsUnavailable_FailsClosed ()
        {
            var result = SceneViewOverlayPolicy.TryEvaluate(
                CreateObservations(
                    popup: SceneViewOverlayPolicy.PresenceObservation.Unavailable),
                out var presentation,
                out var errorMessage);

            Assert.That(result, Is.False);
            Assert.That(presentation, Is.EqualTo(SceneViewOverlayPolicy.ExcludedPresentation.None));
            Assert.That(errorMessage, Does.Contain("Overlay popup visibility capability"));
        }

        [Test]
        [Category("Size.Small")]
        public void TryEvaluate_WhenEveryExcludedPresentationIsProvenAbsent_ReturnsNone ()
        {
            var result = SceneViewOverlayPolicy.TryEvaluate(
                CreateObservations(),
                out var presentation,
                out var errorMessage);

            Assert.That(result, Is.True, errorMessage);
            Assert.That(presentation, Is.EqualTo(SceneViewOverlayPolicy.ExcludedPresentation.None));
        }

        [TestCase(
            nameof(SceneViewOverlayPolicy.PresenceObservation.Displayed),
            nameof(SceneViewOverlayPolicy.PresenceObservation.NotDisplayed),
            false,
            nameof(SceneViewOverlayPolicy.PresenceObservation.NotDisplayed),
            nameof(SceneViewOverlayPolicy.ExcludedPresentation.None),
            nameof(SceneViewOverlayPolicy.ExcludedPresentation.OverlayMenu))]
        [TestCase(
            nameof(SceneViewOverlayPolicy.PresenceObservation.NotDisplayed),
            nameof(SceneViewOverlayPolicy.PresenceObservation.Displayed),
            true,
            nameof(SceneViewOverlayPolicy.PresenceObservation.NotDisplayed),
            nameof(SceneViewOverlayPolicy.ExcludedPresentation.None),
            nameof(SceneViewOverlayPolicy.ExcludedPresentation.OverlayMenu))]
        [TestCase(
            nameof(SceneViewOverlayPolicy.PresenceObservation.NotDisplayed),
            nameof(SceneViewOverlayPolicy.PresenceObservation.Displayed),
            false,
            nameof(SceneViewOverlayPolicy.PresenceObservation.NotDisplayed),
            nameof(SceneViewOverlayPolicy.ExcludedPresentation.None),
            nameof(SceneViewOverlayPolicy.ExcludedPresentation.ConfigurableOverlayPopup))]
        [TestCase(
            nameof(SceneViewOverlayPolicy.PresenceObservation.NotDisplayed),
            nameof(SceneViewOverlayPolicy.PresenceObservation.NotDisplayed),
            false,
            nameof(SceneViewOverlayPolicy.PresenceObservation.Displayed),
            nameof(SceneViewOverlayPolicy.ExcludedPresentation.None),
            nameof(SceneViewOverlayPolicy.ExcludedPresentation.ConfigurableOverlayPanelOrToolbar))]
        [TestCase(
            nameof(SceneViewOverlayPolicy.PresenceObservation.Unavailable),
            nameof(SceneViewOverlayPolicy.PresenceObservation.NotDisplayed),
            false,
            nameof(SceneViewOverlayPolicy.PresenceObservation.NotDisplayed),
            nameof(SceneViewOverlayPolicy.ExcludedPresentation.OverlayMenu),
            nameof(SceneViewOverlayPolicy.ExcludedPresentation.OverlayMenu))]
        [Category("Size.Small")]
        public void TryEvaluate_WhenExcludedPresentationIsDisplayed_ReturnsItsKind (
            string menuName,
            string popupName,
            bool popupContainsOverlayMenu,
            string configurableOverlayName,
            string visualTreePresentationName,
            string expectedName)
        {
            var menu = Enum.Parse<SceneViewOverlayPolicy.PresenceObservation>(menuName);
            var popup = Enum.Parse<SceneViewOverlayPolicy.PresenceObservation>(popupName);
            var configurableOverlay = Enum.Parse<SceneViewOverlayPolicy.PresenceObservation>(
                configurableOverlayName);
            var visualTreePresentation = Enum.Parse<SceneViewOverlayPolicy.ExcludedPresentation>(
                visualTreePresentationName);
            var expected = Enum.Parse<SceneViewOverlayPolicy.ExcludedPresentation>(expectedName);
            var result = SceneViewOverlayPolicy.TryEvaluate(
                new SceneViewOverlayPolicy.ObservationSet(
                    menu,
                    popup,
                    popupContainsOverlayMenu,
                    configurableOverlay,
                    visualTreePresentation),
                out var presentation,
                out var errorMessage);

            Assert.That(result, Is.True, errorMessage);
            Assert.That(presentation, Is.EqualTo(expected));
        }

        private static SceneViewOverlayPolicy.ObservationSet CreateObservations (
            SceneViewOverlayPolicy.PresenceObservation menu = SceneViewOverlayPolicy.PresenceObservation.NotDisplayed,
            SceneViewOverlayPolicy.PresenceObservation popup = SceneViewOverlayPolicy.PresenceObservation.NotDisplayed,
            SceneViewOverlayPolicy.PresenceObservation configurableOverlay = SceneViewOverlayPolicy.PresenceObservation.NotDisplayed)
        {
            return new SceneViewOverlayPolicy.ObservationSet(
                menu,
                popup,
                popupContainsOverlayMenu: false,
                configurableOverlay,
                SceneViewOverlayPolicy.ExcludedPresentation.None);
        }
    }
}
