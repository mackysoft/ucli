using System;
using System.Collections.Generic;
using UnityEditor;

namespace MackySoft.Ucli.Unity.ScreenshotCapture.GameView.Resolution
{
    /// <summary> Validates that a global GameView size-group mutation has one unambiguous window owner. </summary>
    internal static class UnityGameViewWindowSetPolicy
    {
        /// <summary> Requires exactly one live GameView and proves that it is the expected mutation target. </summary>
        public static bool TryValidateExclusiveTarget (
            EditorWindow expectedTarget,
            IReadOnlyList<EditorWindow> liveGameViews,
            out string errorMessage)
        {
            if (expectedTarget == null)
            {
                errorMessage =
                    "The requested-resolution mutation target is not a live GameView.";
                return false;
            }

            if (!TryResolveExclusive(
                liveGameViews,
                out var resolvedTarget,
                out errorMessage))
            {
                return false;
            }

            if (resolvedTarget != expectedTarget)
            {
                errorMessage =
                    "The only live GameView is not the requested-resolution mutation target.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        /// <summary> Resolves the only live GameView eligible for a global size-group mutation. </summary>
        public static bool TryResolveExclusive (
            IReadOnlyList<EditorWindow> liveGameViews,
            out EditorWindow resolvedTarget,
            out string errorMessage)
        {
            if (liveGameViews == null)
            {
                throw new ArgumentNullException(nameof(liveGameViews));
            }

            resolvedTarget = null;
            if (liveGameViews.Count != 1)
            {
                errorMessage =
                    "Requested-resolution capture requires exactly one live GameView before modifying the global size group.";
                return false;
            }

            resolvedTarget = liveGameViews[0];
            if (resolvedTarget == null)
            {
                resolvedTarget = null;
                errorMessage =
                    "Requested-resolution capture requires one live GameView before modifying the global size group.";
                return false;
            }

            errorMessage = null;
            return true;
        }
    }
}
