namespace MackySoft.Ucli.Unity.ScreenshotCapture.SceneView
{
    /// <summary> Decides whether every excluded SceneView presentation is proven absent. </summary>
    internal static class SceneViewOverlayPolicy
    {
        internal enum PresenceObservation
        {
            Unavailable,
            NotDisplayed,
            Displayed,
        }

        internal enum ExcludedPresentation
        {
            None,
            OverlayMenu,
            ConfigurableOverlayPanelOrToolbar,
            ConfigurableOverlayPopup,
        }

        internal readonly struct ObservationSet
        {
            public ObservationSet (
                PresenceObservation menu,
                PresenceObservation popup,
                bool popupContainsOverlayMenu,
                PresenceObservation configurableOverlay,
                ExcludedPresentation visualTreePresentation)
            {
                Menu = menu;
                Popup = popup;
                PopupContainsOverlayMenu = popupContainsOverlayMenu;
                ConfigurableOverlay = configurableOverlay;
                VisualTreePresentation = visualTreePresentation;
            }

            public PresenceObservation Menu { get; }

            public PresenceObservation Popup { get; }

            public bool PopupContainsOverlayMenu { get; }

            public PresenceObservation ConfigurableOverlay { get; }

            public ExcludedPresentation VisualTreePresentation { get; }
        }

        public static bool TryEvaluate (
            ObservationSet observations,
            out ExcludedPresentation excludedPresentation,
            out string errorMessage)
        {
            excludedPresentation = observations.VisualTreePresentation;
            if (excludedPresentation != ExcludedPresentation.None)
            {
                errorMessage = null;
                return true;
            }

            if (observations.Menu == PresenceObservation.Displayed)
            {
                excludedPresentation = ExcludedPresentation.OverlayMenu;
                errorMessage = null;
                return true;
            }

            if (observations.Popup == PresenceObservation.Displayed)
            {
                excludedPresentation = observations.PopupContainsOverlayMenu
                    ? ExcludedPresentation.OverlayMenu
                    : ExcludedPresentation.ConfigurableOverlayPopup;
                errorMessage = null;
                return true;
            }

            if (observations.ConfigurableOverlay == PresenceObservation.Displayed)
            {
                excludedPresentation = ExcludedPresentation.ConfigurableOverlayPanelOrToolbar;
                errorMessage = null;
                return true;
            }

            if (observations.Menu == PresenceObservation.Unavailable)
            {
                errorMessage =
                    "SceneView Overlay Menu visibility capability is unavailable; capture cannot prove excluded chrome is absent.";
                return false;
            }

            if (observations.Popup == PresenceObservation.Unavailable)
            {
                errorMessage =
                    "SceneView Overlay popup visibility capability is unavailable; capture cannot prove excluded chrome is absent.";
                return false;
            }

            if (observations.ConfigurableOverlay == PresenceObservation.Unavailable)
            {
                errorMessage =
                    "SceneView configurable Overlay visibility capability is unavailable; capture cannot prove excluded chrome is absent.";
                return false;
            }

            errorMessage = null;
            return true;
        }
    }
}
