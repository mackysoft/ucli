using System;
using System.Collections;
using System.Collections.Generic;
using MackySoft.Ucli.Unity.ScreenshotCapture.EditorInternals;
using UnityEngine;
using UnityEngine.UIElements;

namespace MackySoft.Ucli.Unity.ScreenshotCapture.SceneView
{
    /// <summary> Observes SceneView chrome without changing the presentation being captured. </summary>
    internal static class UnitySceneViewOverlayProbe
    {
        private const string OverlayMenuTypeFullName = "UnityEditor.Overlays.OverlayMenu";

        private const string OverlayMenuIdentifier = "overlay-menu";

        private const string OverlayPopupIdentifier = "overlay-popup";

        public static bool TryInspect (
            UnityEditor.SceneView sceneView,
            out SceneViewOverlayPolicy.ExcludedPresentation excludedPresentation,
            out string errorMessage)
        {
            excludedPresentation = SceneViewOverlayPolicy.ExcludedPresentation.None;
            if (sceneView == null)
            {
                errorMessage = "SceneView is unavailable for Overlay inspection.";
                return false;
            }

            var canvas = sceneView.overlayCanvas;
            if (canvas == null)
            {
                errorMessage = "SceneView Overlay canvas is unavailable.";
                return false;
            }

            var rootVisualElementProperty = UnityEditorReflection.FindProperty(
                canvas.GetType(),
                "rootVisualElement");
            var cameraViewVisualElementProperty = UnityEditorReflection.FindProperty(
                typeof(UnityEditor.SceneView),
                "cameraViewVisualElement");
            if (rootVisualElementProperty?.GetValue(canvas) is not VisualElement canvasRoot
                || cameraViewVisualElementProperty?.GetValue(sceneView)
                    is not VisualElement cameraViewVisualElement)
            {
                errorMessage = "SceneView Overlay presentation bounds could not be resolved.";
                return false;
            }

            var contentBounds = cameraViewVisualElement.worldBound;
            if (!IsFinite(contentBounds.x)
                || !IsFinite(contentBounds.y)
                || !IsFinitePositive(contentBounds.width)
                || !IsFinitePositive(contentBounds.height))
            {
                errorMessage = "SceneView content bounds are invalid for Overlay exclusion.";
                return false;
            }

            if (!TryFindDisplayedExcludedChrome(
                canvasRoot,
                contentBounds,
                out var visualTreePresentation,
                out errorMessage))
            {
                return false;
            }

            var orientationGizmoField = UnityEditorReflection.FindField(
                typeof(UnityEditor.SceneView),
                "m_OrientationGizmo");
            if (orientationGizmoField?.GetValue(sceneView) is not object includedOrientationGizmo)
            {
                errorMessage = "SceneView included orientation gizmo identity is unavailable.";
                return false;
            }

            var overlayMenuType = typeof(UnityEditor.SceneView).Assembly.GetType(
                OverlayMenuTypeFullName,
                throwOnError: false,
                ignoreCase: false);
            var menuObservation = ObserveMenu(canvas);
            var popupObservation = ObservePopup(
                canvas,
                overlayMenuType,
                out var popupContainsOverlayMenu);
            if (!TryObserveOverlayCollection(
                canvas,
                includedOrientationGizmo,
                overlayMenuType,
                menuObservation,
                out menuObservation,
                out var configurableOverlayObservation,
                out errorMessage))
            {
                return false;
            }

            return SceneViewOverlayPolicy.TryEvaluate(
                new SceneViewOverlayPolicy.ObservationSet(
                    menuObservation,
                    popupObservation,
                    popupContainsOverlayMenu,
                    configurableOverlayObservation,
                    visualTreePresentation),
                out excludedPresentation,
                out errorMessage);
        }

        internal static SceneViewOverlayPolicy.PresenceObservation ObserveMenu (object canvas)
        {
            if (canvas == null)
            {
                return SceneViewOverlayPolicy.PresenceObservation.Unavailable;
            }

            var menuVisibleProperty = UnityEditorReflection.FindProperty(
                canvas.GetType(),
                "menuVisible");
            return menuVisibleProperty?.GetValue(canvas) is bool menuVisible
                ? menuVisible
                    ? SceneViewOverlayPolicy.PresenceObservation.Displayed
                    : SceneViewOverlayPolicy.PresenceObservation.NotDisplayed
                : SceneViewOverlayPolicy.PresenceObservation.Unavailable;
        }

        internal static SceneViewOverlayPolicy.PresenceObservation ObservePopup (
            object canvas,
            Type overlayMenuType,
            out bool popupContainsOverlayMenu)
        {
            popupContainsOverlayMenu = false;
            if (canvas == null)
            {
                return SceneViewOverlayPolicy.PresenceObservation.Unavailable;
            }

            var popupOverlayField = UnityEditorReflection.FindField(
                canvas.GetType(),
                "m_PopupOverlay");
            if (popupOverlayField == null)
            {
                return SceneViewOverlayPolicy.PresenceObservation.Unavailable;
            }

            var popup = popupOverlayField.GetValue(canvas);
            if (popup == null)
            {
                return SceneViewOverlayPolicy.PresenceObservation.NotDisplayed;
            }

            var overlayProperty = UnityEditorReflection.FindProperty(popup.GetType(), "overlay");
            if (overlayProperty?.GetValue(popup) is object overlay)
            {
                popupContainsOverlayMenu = overlayMenuType != null
                    && overlay.GetType() == overlayMenuType;
            }

            return SceneViewOverlayPolicy.PresenceObservation.Displayed;
        }

        internal static bool TryObserveOverlayCollection (
            object canvas,
            object includedOrientationGizmo,
            Type overlayMenuType,
            SceneViewOverlayPolicy.PresenceObservation menuObservation,
            out SceneViewOverlayPolicy.PresenceObservation resolvedMenuObservation,
            out SceneViewOverlayPolicy.PresenceObservation configurableOverlayObservation,
            out string errorMessage)
        {
            resolvedMenuObservation = menuObservation;
            configurableOverlayObservation = SceneViewOverlayPolicy.PresenceObservation.Unavailable;
            if (canvas == null)
            {
                errorMessage = "SceneView Overlay collection capability is unavailable.";
                return false;
            }

            if (includedOrientationGizmo == null)
            {
                errorMessage = "SceneView included orientation gizmo identity is unavailable.";
                return false;
            }

            var overlaysEnabledProperty = UnityEditorReflection.FindProperty(
                canvas.GetType(),
                "overlaysEnabled");
            if (overlaysEnabledProperty?.GetValue(canvas) is not bool overlaysEnabled)
            {
                errorMessage = "SceneView Overlay enabled-state capability is unavailable.";
                return false;
            }

            var overlaysProperty = UnityEditorReflection.FindProperty(canvas.GetType(), "overlays");
            if (overlaysProperty?.GetValue(canvas) is not IEnumerable overlays)
            {
                errorMessage = "SceneView Overlay collection capability is unavailable.";
                return false;
            }

            configurableOverlayObservation = SceneViewOverlayPolicy.PresenceObservation.NotDisplayed;
            foreach (var overlay in overlays)
            {
                if (overlay == null)
                {
                    continue;
                }

                var displayedProperty = UnityEditorReflection.FindProperty(overlay.GetType(), "displayed");
                if (displayedProperty?.GetValue(overlay) is not bool displayed)
                {
                    configurableOverlayObservation = SceneViewOverlayPolicy.PresenceObservation.Unavailable;
                    errorMessage =
                        $"SceneView Overlay item visibility capability is unavailable: {overlay.GetType().FullName}.";
                    return false;
                }

                var effectivelyDisplayed = overlaysEnabled && displayed;
                if (overlayMenuType != null && overlay.GetType() == overlayMenuType)
                {
                    if (effectivelyDisplayed)
                    {
                        resolvedMenuObservation = SceneViewOverlayPolicy.PresenceObservation.Displayed;
                    }
                    else if (resolvedMenuObservation == SceneViewOverlayPolicy.PresenceObservation.Unavailable)
                    {
                        resolvedMenuObservation = SceneViewOverlayPolicy.PresenceObservation.NotDisplayed;
                    }

                    continue;
                }

                if (ReferenceEquals(overlay, includedOrientationGizmo))
                {
                    // Scene orientation is target-included SceneView presentation, not configurable chrome.
                    continue;
                }

                if (effectivelyDisplayed)
                {
                    configurableOverlayObservation = SceneViewOverlayPolicy.PresenceObservation.Displayed;
                }
            }

            errorMessage = null;
            return true;
        }

        internal static bool TryFindDisplayedExcludedChrome (
            VisualElement canvasRoot,
            Rect contentBounds,
            out SceneViewOverlayPolicy.ExcludedPresentation excludedPresentation,
            out string errorMessage)
        {
            excludedPresentation = SceneViewOverlayPolicy.ExcludedPresentation.None;
            if (canvasRoot == null)
            {
                errorMessage = "SceneView Overlay visual tree is unavailable.";
                return false;
            }

            var candidates = new List<VisualElement>();
            CollectExcludedChromeCandidates(canvasRoot, candidates);
            var menuDisplayed = false;
            var popupDisplayed = false;
            foreach (var candidate in candidates)
            {
                if (!IsEffectivelyDisplayed(candidate, canvasRoot))
                {
                    continue;
                }

                var bounds = candidate.worldBound;
                if (!IsFinite(bounds.x)
                    || !IsFinite(bounds.y)
                    || !IsFinitePositive(bounds.width)
                    || !IsFinitePositive(bounds.height))
                {
                    errorMessage = "Displayed SceneView Overlay chrome bounds are unavailable.";
                    return false;
                }

                if (!bounds.Overlaps(contentBounds))
                {
                    continue;
                }

                if (HasVisualElementIdentifier(candidate, OverlayMenuIdentifier))
                {
                    menuDisplayed = true;
                }
                else
                {
                    popupDisplayed = true;
                }
            }

            excludedPresentation = menuDisplayed
                ? SceneViewOverlayPolicy.ExcludedPresentation.OverlayMenu
                : popupDisplayed
                    ? SceneViewOverlayPolicy.ExcludedPresentation.ConfigurableOverlayPopup
                    : SceneViewOverlayPolicy.ExcludedPresentation.None;
            errorMessage = null;
            return true;
        }

        private static void CollectExcludedChromeCandidates (
            VisualElement element,
            ICollection<VisualElement> candidates)
        {
            if (HasVisualElementIdentifier(element, OverlayMenuIdentifier)
                || HasVisualElementIdentifier(element, OverlayPopupIdentifier))
            {
                candidates.Add(element);
            }

            for (var index = 0; index < element.childCount; index++)
            {
                CollectExcludedChromeCandidates(element[index], candidates);
            }
        }

        private static bool HasVisualElementIdentifier (
            VisualElement element,
            string identifier)
        {
            return string.Equals(element.name, identifier, StringComparison.Ordinal)
                || element.ClassListContains(identifier);
        }

        private static bool IsEffectivelyDisplayed (
            VisualElement element,
            VisualElement canvasRoot)
        {
            for (var current = element; current != null; current = current.parent)
            {
                var style = current.resolvedStyle;
                if (style.display == DisplayStyle.None
                    || style.visibility == Visibility.Hidden
                    || style.opacity <= 0f)
                {
                    return false;
                }

                if (ReferenceEquals(current, canvasRoot))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsFinitePositive (float value)
        {
            return value > 0f && IsFinite(value);
        }

        private static bool IsFinite (float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
