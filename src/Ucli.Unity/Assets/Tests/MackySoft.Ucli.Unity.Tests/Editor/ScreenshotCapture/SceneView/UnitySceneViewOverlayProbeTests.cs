using System;
using System.Collections;
using MackySoft.Ucli.Unity.ScreenshotCapture.SceneView;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnitySceneViewOverlayProbeTests
    {
        [Test]
        [Category("Size.Small")]
        public void ObservePopup_WithoutPopupField_ReturnsUnavailable ()
        {
            var observation = UnitySceneViewOverlayProbe.ObservePopup(
                new CanvasWithoutPopupCapability(),
                overlayMenuType: null,
                out var containsOverlayMenu);

            Assert.That(observation, Is.EqualTo(SceneViewOverlayPolicy.PresenceObservation.Unavailable));
            Assert.That(containsOverlayMenu, Is.False);
        }

        [Test]
        [Category("Size.Small")]
        public void ObservePopup_WithNullPopupField_ProvesPopupAbsent ()
        {
            var observation = UnitySceneViewOverlayProbe.ObservePopup(
                new CanvasWithPopupCapability(popup: null),
                overlayMenuType: null,
                out var containsOverlayMenu);

            Assert.That(observation, Is.EqualTo(SceneViewOverlayPolicy.PresenceObservation.NotDisplayed));
            Assert.That(containsOverlayMenu, Is.False);
        }

        [Test]
        [Category("Size.Small")]
        public void ObservePopup_WithNonNullPopupField_ReportsDisplayedPopup ()
        {
            var observation = UnitySceneViewOverlayProbe.ObservePopup(
                new CanvasWithPopupCapability(new object()),
                overlayMenuType: null,
                out var containsOverlayMenu);

            Assert.That(observation, Is.EqualTo(SceneViewOverlayPolicy.PresenceObservation.Displayed));
            Assert.That(containsOverlayMenu, Is.False);
        }

        [Test]
        [Category("Size.Small")]
        public void ObservePopup_WithModernOverlayMenuPopup_ReportsOverlayMenu ()
        {
            var observation = UnitySceneViewOverlayProbe.ObservePopup(
                new CanvasWithPopupCapability(
                    new PopupWithOverlay(new OverlayMenu(displayed: false))),
                typeof(OverlayMenu),
                out var containsOverlayMenu);

            Assert.That(observation, Is.EqualTo(SceneViewOverlayPolicy.PresenceObservation.Displayed));
            Assert.That(containsOverlayMenu, Is.True);
        }

        [Test]
        [Category("Size.Small")]
        public void ObservePopup_WithSameNamedButDifferentMenuIdentity_DoesNotTrustTypeName ()
        {
            var observation = UnitySceneViewOverlayProbe.ObservePopup(
                new CanvasWithPopupCapability(
                    new PopupWithOverlay(new CustomOverlayTypes.OverlayMenu(displayed: false))),
                typeof(OverlayMenu),
                out var containsOverlayMenu);

            Assert.That(observation, Is.EqualTo(SceneViewOverlayPolicy.PresenceObservation.Displayed));
            Assert.That(containsOverlayMenu, Is.False);
        }

        [Test]
        [Category("Size.Small")]
        public void ObserveMenu_WithoutMenuVisibleProperty_ReturnsUnavailable ()
        {
            var observation = UnitySceneViewOverlayProbe.ObserveMenu(
                new CanvasWithoutPopupCapability());

            Assert.That(observation, Is.EqualTo(SceneViewOverlayPolicy.PresenceObservation.Unavailable));
        }

        [TestCase(false, nameof(SceneViewOverlayPolicy.PresenceObservation.NotDisplayed))]
        [TestCase(true, nameof(SceneViewOverlayPolicy.PresenceObservation.Displayed))]
        [Category("Size.Small")]
        public void ObserveMenu_WithMenuVisibleProperty_ReportsItsState (
            bool menuVisible,
            string expectedName)
        {
            var expected = Enum.Parse<SceneViewOverlayPolicy.PresenceObservation>(expectedName);
            var observation = UnitySceneViewOverlayProbe.ObserveMenu(
                new CanvasWithMenuCapability(menuVisible));

            Assert.That(observation, Is.EqualTo(expected));
        }

        [Test]
        [Category("Size.Small")]
        public void TryFindDisplayedExcludedChrome_WithHiddenMenuHint_DoesNotProveAbsence ()
        {
            var root = new VisualElement();
            var hiddenMenuHint = new VisualElement { name = "overlay-menu" };
            hiddenMenuHint.style.display = DisplayStyle.None;
            root.Add(hiddenMenuHint);

            var inspected = UnitySceneViewOverlayProbe.TryFindDisplayedExcludedChrome(
                root,
                new Rect(0f, 0f, 100f, 100f),
                out var visualTreePresentation,
                out var inspectionError);
            var evaluated = SceneViewOverlayPolicy.TryEvaluate(
                new SceneViewOverlayPolicy.ObservationSet(
                    SceneViewOverlayPolicy.PresenceObservation.Unavailable,
                    SceneViewOverlayPolicy.PresenceObservation.NotDisplayed,
                    popupContainsOverlayMenu: false,
                    SceneViewOverlayPolicy.PresenceObservation.NotDisplayed,
                    visualTreePresentation),
                out _,
                out var policyError);

            Assert.That(inspected, Is.True, inspectionError);
            Assert.That(
                visualTreePresentation,
                Is.EqualTo(SceneViewOverlayPolicy.ExcludedPresentation.None));
            Assert.That(evaluated, Is.False);
            Assert.That(policyError, Does.Contain("Overlay Menu visibility capability"));
        }

        [Test]
        [Category("Size.Small")]
        public void TryObserveOverlayCollection_WithOrientationGizmoOnly_ReportsNoConfigurableOverlay ()
        {
            var orientationGizmo = new SceneOrientationGizmo(displayed: true);
            var result = UnitySceneViewOverlayProbe.TryObserveOverlayCollection(
                new CanvasWithOverlayCapability(
                    overlaysEnabled: true,
                    orientationGizmo),
                orientationGizmo,
                overlayMenuType: null,
                SceneViewOverlayPolicy.PresenceObservation.NotDisplayed,
                out var menu,
                out var configurableOverlay,
                out var errorMessage);

            Assert.That(result, Is.True, errorMessage);
            Assert.That(menu, Is.EqualTo(SceneViewOverlayPolicy.PresenceObservation.NotDisplayed));
            Assert.That(
                configurableOverlay,
                Is.EqualTo(SceneViewOverlayPolicy.PresenceObservation.NotDisplayed));
        }

        [Test]
        [Category("Size.Small")]
        public void TryObserveOverlayCollection_WithSameNamedOrientationGizmoInstance_ReportsConfigurableOverlay ()
        {
            var result = UnitySceneViewOverlayProbe.TryObserveOverlayCollection(
                new CanvasWithOverlayCapability(
                    overlaysEnabled: true,
                    new SceneOrientationGizmo(displayed: true)),
                new SceneOrientationGizmo(displayed: true),
                overlayMenuType: null,
                SceneViewOverlayPolicy.PresenceObservation.NotDisplayed,
                out _,
                out var configurableOverlay,
                out var errorMessage);

            Assert.That(result, Is.True, errorMessage);
            Assert.That(
                configurableOverlay,
                Is.EqualTo(SceneViewOverlayPolicy.PresenceObservation.Displayed));
        }

        [Test]
        [Category("Size.Small")]
        public void TryObserveOverlayCollection_WithoutOrientationGizmoIdentity_FailsClosed ()
        {
            var result = UnitySceneViewOverlayProbe.TryObserveOverlayCollection(
                new CanvasWithOverlayCapability(
                    overlaysEnabled: true,
                    new ConfigurableOverlay(displayed: false)),
                includedOrientationGizmo: null,
                overlayMenuType: null,
                SceneViewOverlayPolicy.PresenceObservation.NotDisplayed,
                out _,
                out var configurableOverlay,
                out var errorMessage);

            Assert.That(result, Is.False);
            Assert.That(
                configurableOverlay,
                Is.EqualTo(SceneViewOverlayPolicy.PresenceObservation.Unavailable));
            Assert.That(errorMessage, Does.Contain("orientation gizmo identity"));
        }

        [Test]
        [Category("Size.Small")]
        public void TryObserveOverlayCollection_WithDisplayedPanel_ReportsConfigurableOverlay ()
        {
            var result = UnitySceneViewOverlayProbe.TryObserveOverlayCollection(
                new CanvasWithOverlayCapability(
                    overlaysEnabled: true,
                    new ConfigurableOverlay(displayed: true)),
                new SceneOrientationGizmo(displayed: false),
                overlayMenuType: null,
                SceneViewOverlayPolicy.PresenceObservation.NotDisplayed,
                out var menu,
                out var configurableOverlay,
                out var errorMessage);

            Assert.That(result, Is.True, errorMessage);
            Assert.That(menu, Is.EqualTo(SceneViewOverlayPolicy.PresenceObservation.NotDisplayed));
            Assert.That(
                configurableOverlay,
                Is.EqualTo(SceneViewOverlayPolicy.PresenceObservation.Displayed));
        }

        [Test]
        [Category("Size.Small")]
        public void TryObserveOverlayCollection_WithDisplayedModernMenu_ReportsOverlayMenu ()
        {
            var result = UnitySceneViewOverlayProbe.TryObserveOverlayCollection(
                new CanvasWithOverlayCapability(
                    overlaysEnabled: true,
                    new OverlayMenu(displayed: true)),
                new SceneOrientationGizmo(displayed: false),
                typeof(OverlayMenu),
                SceneViewOverlayPolicy.PresenceObservation.Unavailable,
                out var menu,
                out var configurableOverlay,
                out var errorMessage);

            Assert.That(result, Is.True, errorMessage);
            Assert.That(menu, Is.EqualTo(SceneViewOverlayPolicy.PresenceObservation.Displayed));
            Assert.That(
                configurableOverlay,
                Is.EqualTo(SceneViewOverlayPolicy.PresenceObservation.NotDisplayed));
        }

        [Test]
        [Category("Size.Small")]
        public void TryObserveOverlayCollection_WithHiddenModernMenu_ProvesMenuAbsent ()
        {
            var result = UnitySceneViewOverlayProbe.TryObserveOverlayCollection(
                new CanvasWithOverlayCapability(
                    overlaysEnabled: true,
                    new OverlayMenu(displayed: false)),
                new SceneOrientationGizmo(displayed: false),
                typeof(OverlayMenu),
                SceneViewOverlayPolicy.PresenceObservation.Unavailable,
                out var menu,
                out var configurableOverlay,
                out var errorMessage);

            Assert.That(result, Is.True, errorMessage);
            Assert.That(menu, Is.EqualTo(SceneViewOverlayPolicy.PresenceObservation.NotDisplayed));
            Assert.That(
                configurableOverlay,
                Is.EqualTo(SceneViewOverlayPolicy.PresenceObservation.NotDisplayed));
        }

        [Test]
        [Category("Size.Small")]
        public void TryObserveOverlayCollection_WithSameNamedCustomMenu_ReportsConfigurableOverlay ()
        {
            var result = UnitySceneViewOverlayProbe.TryObserveOverlayCollection(
                new CanvasWithOverlayCapability(
                    overlaysEnabled: true,
                    new CustomOverlayTypes.OverlayMenu(displayed: true)),
                new SceneOrientationGizmo(displayed: false),
                typeof(OverlayMenu),
                SceneViewOverlayPolicy.PresenceObservation.Unavailable,
                out var menu,
                out var configurableOverlay,
                out var errorMessage);

            Assert.That(result, Is.True, errorMessage);
            Assert.That(menu, Is.EqualTo(SceneViewOverlayPolicy.PresenceObservation.Unavailable));
            Assert.That(
                configurableOverlay,
                Is.EqualTo(SceneViewOverlayPolicy.PresenceObservation.Displayed));
        }

        [Test]
        [Category("Size.Small")]
        public void TryObserveOverlayCollection_WithoutItemVisibilityCapability_FailsClosed ()
        {
            var result = UnitySceneViewOverlayProbe.TryObserveOverlayCollection(
                new CanvasWithOverlayCapability(
                    overlaysEnabled: true,
                    new OverlayWithoutDisplayedProperty()),
                new SceneOrientationGizmo(displayed: false),
                overlayMenuType: null,
                SceneViewOverlayPolicy.PresenceObservation.NotDisplayed,
                out _,
                out var configurableOverlay,
                out var errorMessage);

            Assert.That(result, Is.False);
            Assert.That(
                configurableOverlay,
                Is.EqualTo(SceneViewOverlayPolicy.PresenceObservation.Unavailable));
            Assert.That(errorMessage, Does.Contain("item visibility capability"));
        }

        [Test]
        [Category("Size.Small")]
        public void TryObserveOverlayCollection_WithoutEnabledStateCapability_FailsClosed ()
        {
            var result = UnitySceneViewOverlayProbe.TryObserveOverlayCollection(
                new CanvasWithoutOverlayCapabilities(),
                new SceneOrientationGizmo(displayed: false),
                overlayMenuType: null,
                SceneViewOverlayPolicy.PresenceObservation.NotDisplayed,
                out _,
                out var configurableOverlay,
                out var errorMessage);

            Assert.That(result, Is.False);
            Assert.That(
                configurableOverlay,
                Is.EqualTo(SceneViewOverlayPolicy.PresenceObservation.Unavailable));
            Assert.That(errorMessage, Does.Contain("enabled-state capability"));
        }

        [Test]
        [Category("Size.Small")]
        public void TryObserveOverlayCollection_WithoutCollectionCapability_FailsClosed ()
        {
            var result = UnitySceneViewOverlayProbe.TryObserveOverlayCollection(
                new CanvasWithoutOverlayCollection(),
                new SceneOrientationGizmo(displayed: false),
                overlayMenuType: null,
                SceneViewOverlayPolicy.PresenceObservation.NotDisplayed,
                out _,
                out var configurableOverlay,
                out var errorMessage);

            Assert.That(result, Is.False);
            Assert.That(
                configurableOverlay,
                Is.EqualTo(SceneViewOverlayPolicy.PresenceObservation.Unavailable));
            Assert.That(errorMessage, Does.Contain("collection capability"));
        }

        private sealed class CanvasWithoutPopupCapability
        {
        }

        private sealed class CanvasWithPopupCapability
        {
            private readonly object m_PopupOverlay;

            public CanvasWithPopupCapability (object popup)
            {
                m_PopupOverlay = popup;
            }

            public object PopupOverlay => m_PopupOverlay;
        }

        private sealed class PopupWithOverlay
        {
            public PopupWithOverlay (object overlayValue)
            {
                overlay = overlayValue;
            }

            public object overlay { get; }
        }

        private sealed class CanvasWithMenuCapability
        {
            public CanvasWithMenuCapability (bool menuVisibleValue)
            {
                menuVisible = menuVisibleValue;
            }

            public bool menuVisible { get; }
        }

        private sealed class CanvasWithOverlayCapability
        {
            public CanvasWithOverlayCapability (bool overlaysEnabled, params object[] overlayItems)
            {
                this.overlaysEnabled = overlaysEnabled;
                overlays = overlayItems;
            }

            public bool overlaysEnabled { get; }

            public IEnumerable overlays { get; }
        }

        private sealed class CanvasWithoutOverlayCapabilities
        {
        }

        private sealed class CanvasWithoutOverlayCollection
        {
            public bool overlaysEnabled => true;
        }

        private sealed class SceneOrientationGizmo
        {
            public SceneOrientationGizmo (bool displayed)
            {
                this.displayed = displayed;
            }

            public bool displayed { get; }
        }

        private sealed class ConfigurableOverlay
        {
            public ConfigurableOverlay (bool displayed)
            {
                this.displayed = displayed;
            }

            public bool displayed { get; }
        }

        private sealed class OverlayMenu
        {
            public OverlayMenu (bool displayed)
            {
                this.displayed = displayed;
            }

            public bool displayed { get; }
        }

        private static class CustomOverlayTypes
        {
            public sealed class OverlayMenu
            {
                public OverlayMenu (bool displayed)
                {
                    this.displayed = displayed;
                }

                public bool displayed { get; }
            }
        }

        private sealed class OverlayWithoutDisplayedProperty
        {
        }
    }
}
