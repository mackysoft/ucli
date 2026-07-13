using System;
using System.Collections.Generic;

namespace MackySoft.Ucli.Unity.ScreenshotCapture.GameView.Resolution
{
    /// <summary> Validates that a global GameView size-group mutation has one unambiguous window owner. </summary>
    internal static class UnityGameViewWindowSetPolicy
    {
        /// <summary> Requires exactly one live GameView and proves that it is the expected mutation target. </summary>
        public static bool TryValidateExclusiveTarget (
            int expectedTargetInstanceId,
            IReadOnlyList<int> liveGameViewInstanceIds,
            out string errorMessage)
        {
            if (!TryResolveExclusive(
                liveGameViewInstanceIds,
                out var resolvedInstanceId,
                out errorMessage))
            {
                return false;
            }

            if (resolvedInstanceId != expectedTargetInstanceId)
            {
                errorMessage =
                    "The only live GameView is not the requested-resolution mutation target.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        /// <summary> Resolves the only live GameView identity eligible for a global size-group mutation. </summary>
        public static bool TryResolveExclusive (
            IReadOnlyList<int> liveGameViewInstanceIds,
            out int resolvedInstanceId,
            out string errorMessage)
        {
            if (liveGameViewInstanceIds == null)
            {
                throw new ArgumentNullException(nameof(liveGameViewInstanceIds));
            }

            resolvedInstanceId = default;
            if (liveGameViewInstanceIds.Count != 1)
            {
                errorMessage =
                    "Requested-resolution capture requires exactly one live GameView before modifying the global size group.";
                return false;
            }

            resolvedInstanceId = liveGameViewInstanceIds[0];
            errorMessage = null;
            return true;
        }
    }
}
