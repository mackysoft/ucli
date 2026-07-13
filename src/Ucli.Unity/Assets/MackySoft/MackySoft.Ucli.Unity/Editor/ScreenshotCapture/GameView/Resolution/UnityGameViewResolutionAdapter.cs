using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Unity.ScreenshotCapture.EditorInternals;
using UnityEditor;
using UnityEngine;

namespace MackySoft.Ucli.Unity.ScreenshotCapture.GameView.Resolution
{
    /// <summary> Owns the capability-probed Unity boundary for temporary GameView resolution transactions. </summary>
    internal sealed class UnityGameViewResolutionAdapter
    {
        private readonly Assembly unityEditorAssembly = typeof(EditorWindow).Assembly;

        private readonly IUnityScreenshotResolutionOrphanCleaner orphanCleaner;

        public UnityGameViewResolutionAdapter (IUnityScreenshotResolutionOrphanCleaner orphanCleaner)
        {
            this.orphanCleaner = orphanCleaner ?? throw new ArgumentNullException(nameof(orphanCleaner));
        }

        /// <summary> Retries cleanup only after the caller has excluded live transaction ownership. </summary>
        internal bool TryCleanupPendingOwnership (out string errorMessage)
        {
            return orphanCleaner.TryCleanup(out errorMessage);
        }

        /// <summary> Starts an unsaved, request-owned fixed-resolution transaction. </summary>
        public bool TryBegin (
            EditorWindow gameView,
            int width,
            int height,
            GameViewResolutionLease.TryAcquirePresentationRecoveryOwnership
                tryAcquirePresentationRecoveryOwnership,
            out GameViewResolutionLease lease,
            out UcliCode? errorCode,
            out string errorMessage)
        {
            if (tryAcquirePresentationRecoveryOwnership == null)
            {
                throw new ArgumentNullException(nameof(tryAcquirePresentationRecoveryOwnership));
            }

            lease = null;
            errorCode = null;
            var gameViewType = unityEditorAssembly.GetType("UnityEditor.GameView");
            if (width < 10 || height < 10)
            {
                errorCode = ScreenshotErrorCodes.ScreenshotRequestedSizeUnsupported;
                errorMessage = "Requested GameView resolution is outside Unity's fixed-resolution range.";
                return false;
            }

            if (gameView == null
                || gameViewType == null
                || gameView.GetType() != gameViewType)
            {
                errorCode = ScreenshotErrorCodes.ScreenshotCaptureUnsupported;
                errorMessage =
                    "Requested-resolution capture requires one exact Unity GameView target.";
                return false;
            }

            if (!TryValidateExclusiveGameView(
                gameViewType,
                gameView,
                out errorMessage))
            {
                errorCode = ScreenshotErrorCodes.ScreenshotCaptureUnsupported;
                return false;
            }

            if (!orphanCleaner.TryCleanup(out var cleanupError))
            {
                errorCode = ScreenshotErrorCodes.ScreenshotCaptureUnsupported;
                errorMessage =
                    $"Temporary GameView resolution state is not safe to modify. {cleanupError}";
                return false;
            }

            if (!TryValidateExclusiveGameView(
                gameViewType,
                gameView,
                out errorMessage))
            {
                errorCode = ScreenshotErrorCodes.ScreenshotCaptureUnsupported;
                return false;
            }

            GameViewResolutionLease candidateLease = null;
            ResolutionGroup group = null;
            object temporarySize = null;
            var originalIndex = default(int);
            var originalCount = default(int);
            object originalSize = null;
            string temporaryLabel = null;
            try
            {
                if (!TryResolveGroup(
                    gameView,
                    out group,
                    out var fixedResolutionKind,
                    out var sizeConstructor,
                    out var addCustomSize,
                    out errorMessage))
                {
                    errorCode = ScreenshotErrorCodes.ScreenshotRequestedSizeUnsupported;
                    return false;
                }

                originalCount = group.GetTotalCount();
                if (!group.TryGetSelectedIndex(gameView, out originalIndex)
                    || originalIndex < 0
                    || originalIndex >= originalCount)
                {
                    errorCode = ScreenshotErrorCodes.ScreenshotRequestedSizeUnsupported;
                    errorMessage = "Unity GameView resolution selection is unavailable.";
                    return false;
                }

                originalSize = group.GetSize(originalIndex);
                if (originalSize == null)
                {
                    errorCode = ScreenshotErrorCodes.ScreenshotRequestedSizeUnsupported;
                    errorMessage = "Unity GameView selected resolution identity is unavailable.";
                    return false;
                }

                if (!group.TryValidateExclusiveGameView(gameView, out errorMessage))
                {
                    errorCode = ScreenshotErrorCodes.ScreenshotCaptureUnsupported;
                    return false;
                }

                temporaryLabel = UnityScreenshotResolutionLeaseRegistry.CreateLabel();
                UnityScreenshotResolutionLeaseRegistry.Register(
                    new UnityScreenshotResolutionLeaseRegistry.OwnedResolution(
                        temporaryLabel,
                        width,
                        height,
                        group.GroupTypeName,
                        originalIndex));

                temporarySize = sizeConstructor.Invoke(new object[]
                {
                    fixedResolutionKind,
                    width,
                    height,
                    temporaryLabel,
                });
                addCustomSize.Invoke(group.Group, new[] { temporarySize });
                if (group.GetTotalCount() != originalCount + 1
                    || !ReferenceEquals(group.GetSize(originalCount), temporarySize))
                {
                    TryRollbackRegistration(
                        gameView,
                        group,
                        temporarySize,
                        originalIndex,
                        originalCount,
                        originalSize,
                        temporaryLabel);
                    errorMessage =
                        "Unity did not append the request-owned GameView resolution exactly once.";
                    errorCode = ScreenshotErrorCodes.ScreenshotRequestedSizeUnsupported;
                    return false;
                }

                candidateLease = new GameViewResolutionLease(
                    gameView,
                    group,
                    new GameViewResolutionLease.OriginalResolutionState(
                        originalIndex,
                        originalCount,
                        originalSize),
                    new GameViewResolutionLease.TemporaryResolutionState(
                        temporaryLabel,
                        originalCount,
                        temporarySize));
                if (!TryActivateLease(
                    candidateLease,
                    tryAcquirePresentationRecoveryOwnership,
                    out lease,
                    out errorMessage))
                {
                    errorCode = ScreenshotErrorCodes.ScreenshotRequestedSizeUnsupported;
                    return false;
                }

                errorCode = null;
                errorMessage = null;
                return true;
            }
            catch (Exception exception)
            {
                string recoveryError = null;
                if (candidateLease != null)
                {
                    TryRecoverFailedLease(
                        candidateLease,
                        tryAcquirePresentationRecoveryOwnership,
                        out recoveryError);
                }
                else if (group != null && temporaryLabel != null)
                {
                    if (!TryRollbackRegistration(
                        gameView,
                        group,
                        temporarySize,
                        originalIndex,
                        originalCount,
                        originalSize,
                        temporaryLabel))
                    {
                        recoveryError =
                            "The request-owned GameView resolution could not be rolled back immediately.";
                    }
                }

                lease = null;
                errorCode = ScreenshotErrorCodes.ScreenshotRequestedSizeUnsupported;
                errorMessage =
                    $"Unity GameView resolution transaction failed. {UnityEditorReflection.UnwrapInvocationException(exception).Message}"
                    + (recoveryError == null ? string.Empty : $" {recoveryError}");
                return false;
            }
        }

        /// <summary> Activates one candidate lease and transfers it only after mutation succeeds. </summary>
        internal static bool TryActivateLease (
            GameViewResolutionLease candidate,
            GameViewResolutionLease.TryAcquirePresentationRecoveryOwnership
                tryAcquirePresentationRecoveryOwnership,
            out GameViewResolutionLease lease,
            out string errorMessage)
        {
            if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            if (tryAcquirePresentationRecoveryOwnership == null)
            {
                throw new ArgumentNullException(nameof(tryAcquirePresentationRecoveryOwnership));
            }

            lease = null;
            if (candidate.TryActivate(out var activationError))
            {
                lease = candidate;
                errorMessage = null;
                return true;
            }

            if (!TryRecoverFailedLease(
                candidate,
                tryAcquirePresentationRecoveryOwnership,
                out var recoveryError))
            {
                errorMessage = $"{activationError} {recoveryError}";
                return false;
            }

            errorMessage = activationError;
            return false;
        }

        /// <summary> Returns every live EditorWindow whose concrete type is the probed GameView type. </summary>
        internal static IReadOnlyList<EditorWindow> FindLiveExactGameViews (Type gameViewType)
        {
            if (gameViewType == null)
            {
                throw new ArgumentNullException(nameof(gameViewType));
            }

            return Resources.FindObjectsOfTypeAll(gameViewType)
                .OfType<EditorWindow>()
                .Where(view => view != null && view.GetType() == gameViewType)
                .OrderBy(view => view.GetInstanceID())
                .ToArray();
        }

        private static bool TryRecoverFailedLease (
            GameViewResolutionLease candidate,
            GameViewResolutionLease.TryAcquirePresentationRecoveryOwnership
                tryAcquirePresentationRecoveryOwnership,
            out string errorMessage)
        {
            string lastError = null;
            for (var attempt = 0; attempt < 3 && candidate.CanRetryRestore; attempt++)
            {
                var outcome = candidate.TryRestore(
                    tryAcquirePresentationRecoveryOwnership,
                    out lastError);
                if (outcome != GameViewResolutionLease.RestoreOutcome.Retryable)
                {
                    errorMessage = null;
                    return true;
                }
            }

            if (candidate.CanRetryRestore)
            {
                candidate.ScheduleDeferredRecovery(tryAcquirePresentationRecoveryOwnership);
                errorMessage =
                    $"The failed GameView resolution start was retained for deferred recovery. {lastError}";
                return false;
            }

            errorMessage = null;
            return true;
        }

        private static bool TryValidateExclusiveGameView (
            Type gameViewType,
            EditorWindow expectedTarget,
            out string errorMessage)
        {
            if (expectedTarget == null || expectedTarget.GetType() != gameViewType)
            {
                errorMessage = "The requested-resolution GameView target is unavailable.";
                return false;
            }

            var liveInstanceIds = FindLiveExactGameViews(gameViewType)
                .Select(view => view.GetInstanceID())
                .ToArray();
            return UnityGameViewWindowSetPolicy.TryValidateExclusiveTarget(
                expectedTarget.GetInstanceID(),
                liveInstanceIds,
                out errorMessage);
        }

        private bool TryResolveGroup (
            EditorWindow gameView,
            out ResolutionGroup group,
            out object fixedResolutionKind,
            out ConstructorInfo sizeConstructor,
            out MethodInfo addCustomSize,
            out string errorMessage)
        {
            group = null;
            fixedResolutionKind = null;
            sizeConstructor = null;
            addCustomSize = null;
            var gameViewType = unityEditorAssembly.GetType("UnityEditor.GameView");
            var gameViewSizesType = unityEditorAssembly.GetType("UnityEditor.GameViewSizes");
            var gameViewSizeType = unityEditorAssembly.GetType("UnityEditor.GameViewSize");
            var gameViewSizeKindType = unityEditorAssembly.GetType("UnityEditor.GameViewSizeType");
            if (gameViewType == null
                || gameViewSizesType == null
                || gameViewSizeType == null
                || gameViewSizeKindType == null
                || gameView.GetType() != gameViewType)
            {
                errorMessage = "Unity GameView resolution adapter types are unavailable.";
                return false;
            }

            var instanceProperty = UnityEditorReflection.FindProperty(
                gameViewSizesType,
                "instance",
                UnityEditorReflection.StaticMembers);
            var currentGroupProperty = UnityEditorReflection.FindProperty(
                gameViewSizesType,
                "currentGroup");
            var currentGroupTypeProperty = UnityEditorReflection.FindProperty(
                gameViewSizesType,
                "currentGroupType");
            var selectedSizeIndexProperty = UnityEditorReflection.FindProperty(
                gameViewType,
                "selectedSizeIndex");
            var selectionCallback = UnityEditorReflection.FindMethod(
                gameViewType,
                "SizeSelectionCallback",
                new[] { typeof(int), typeof(object) });
            fixedResolutionKind = Enum.Parse(gameViewSizeKindType, "FixedResolution");
            sizeConstructor = gameViewSizeType.GetConstructor(
                UnityEditorReflection.InstanceMembers,
                binder: null,
                new[] { gameViewSizeKindType, typeof(int), typeof(int), typeof(string) },
                modifiers: null);
            var sizesInstance = instanceProperty?.GetValue(obj: null);
            var groupObject = sizesInstance == null
                ? null
                : currentGroupProperty?.GetValue(sizesInstance);
            var groupTypeValue = sizesInstance == null
                ? null
                : currentGroupTypeProperty?.GetValue(sizesInstance);
            if (sizesInstance == null
                || groupObject == null
                || groupTypeValue == null
                || currentGroupProperty == null
                || currentGroupTypeProperty == null
                || selectedSizeIndexProperty == null
                || selectionCallback == null
                || sizeConstructor == null)
            {
                errorMessage = "Unity GameView resolution adapter members are unavailable.";
                return false;
            }

            var groupType = groupObject.GetType();
            var getTotalCount = UnityEditorReflection.FindMethod(
                groupType,
                "GetTotalCount",
                Type.EmptyTypes);
            var getGameViewSize = UnityEditorReflection.FindMethod(
                groupType,
                "GetGameViewSize",
                new[] { typeof(int) });
            addCustomSize = UnityEditorReflection.FindMethod(
                groupType,
                "AddCustomSize",
                new[] { gameViewSizeType });
            var removeCustomSize = UnityEditorReflection.FindMethod(
                groupType,
                "RemoveCustomSize",
                new[] { typeof(int) });
            if (getTotalCount == null
                || getGameViewSize == null
                || addCustomSize == null
                || removeCustomSize == null)
            {
                errorMessage = "Unity GameView resolution group members are unavailable.";
                return false;
            }

            group = new ResolutionGroup(
                sizesInstance,
                currentGroupProperty,
                currentGroupTypeProperty,
                groupObject,
                groupTypeValue.ToString(),
                selectedSizeIndexProperty,
                selectionCallback,
                getTotalCount,
                getGameViewSize,
                removeCustomSize);
            errorMessage = null;
            return true;
        }

        private static bool TryRollbackRegistration (
            EditorWindow gameView,
            ResolutionGroup group,
            object temporarySize,
            int originalIndex,
            int originalCount,
            object originalSize,
            string temporaryLabel)
        {
            try
            {
                if (!group.TryValidateExclusiveGameView(gameView, out _))
                {
                    return false;
                }

                var count = group.GetTotalCount();
                if (count == originalCount + 1)
                {
                    if (temporarySize == null
                        || !ReferenceEquals(group.GetSize(originalCount), temporarySize))
                    {
                        return false;
                    }

                    group.Remove(originalCount);
                }
                else if (count != originalCount)
                {
                    return false;
                }

                if (group.GetTotalCount() != originalCount
                    || originalIndex < 0
                    || originalIndex >= originalCount
                    || !ReferenceEquals(group.GetSize(originalIndex), originalSize))
                {
                    return false;
                }

                if (temporarySize != null)
                {
                    for (var index = 0; index < originalCount; index++)
                    {
                        if (ReferenceEquals(group.GetSize(index), temporarySize))
                        {
                            return false;
                        }
                    }
                }

                return UnityScreenshotResolutionLeaseRegistry.TryUnregister(
                    temporaryLabel,
                    out _);
            }
            catch
            {
                // Keep the ownership marker so startup recovery can fail closed or remove the exact orphan later.
                return false;
            }
        }

        /// <summary> Encapsulates the reflected GameView size-group operations shared by one transaction. </summary>
        internal sealed class ResolutionGroup
        {
            private readonly object sizesInstance;
            private readonly PropertyInfo currentGroupProperty;
            private readonly PropertyInfo currentGroupTypeProperty;
            private readonly PropertyInfo selectedSizeIndexProperty;
            private readonly MethodInfo selectionCallback;
            private readonly MethodInfo getTotalCount;
            private readonly MethodInfo getGameViewSize;
            private readonly MethodInfo removeCustomSize;

            public ResolutionGroup (
                object sizesInstance,
                PropertyInfo currentGroupProperty,
                PropertyInfo currentGroupTypeProperty,
                object group,
                string groupTypeName,
                PropertyInfo selectedSizeIndexProperty,
                MethodInfo selectionCallback,
                MethodInfo getTotalCount,
                MethodInfo getGameViewSize,
                MethodInfo removeCustomSize)
            {
                this.sizesInstance = sizesInstance;
                this.currentGroupProperty = currentGroupProperty;
                this.currentGroupTypeProperty = currentGroupTypeProperty;
                Group = group;
                GroupTypeName = groupTypeName;
                this.selectedSizeIndexProperty = selectedSizeIndexProperty;
                this.selectionCallback = selectionCallback;
                this.getTotalCount = getTotalCount;
                this.getGameViewSize = getGameViewSize;
                this.removeCustomSize = removeCustomSize;
            }

            public object Group { get; }

            public string GroupTypeName { get; }

            public bool IsCurrent => ReferenceEquals(currentGroupProperty.GetValue(sizesInstance), Group)
                && string.Equals(
                    currentGroupTypeProperty.GetValue(sizesInstance)?.ToString(),
                    GroupTypeName,
                    StringComparison.Ordinal);

            public int GetTotalCount ()
            {
                return getTotalCount.Invoke(Group, parameters: null) is int count
                    ? count
                    : throw new InvalidOperationException("GameView resolution collection count is unavailable.");
            }

            public object GetSize (int index)
            {
                return getGameViewSize.Invoke(Group, new object[] { index });
            }

            public bool TryGetSelectedIndex (EditorWindow gameView, out int selectedIndex)
            {
                if (selectedSizeIndexProperty.GetValue(gameView) is int value)
                {
                    selectedIndex = value;
                    return true;
                }

                selectedIndex = default;
                return false;
            }

            public bool TryValidateExclusiveGameView (
                EditorWindow expectedTarget,
                out string errorMessage)
            {
                if (expectedTarget == null)
                {
                    errorMessage = "The requested-resolution GameView target is unavailable.";
                    return false;
                }

                return UnityGameViewResolutionAdapter.TryValidateExclusiveGameView(
                    expectedTarget.GetType(),
                    expectedTarget,
                    out errorMessage);
            }

            public void Select (EditorWindow gameView, int index)
            {
                selectionCallback.Invoke(gameView, new object[] { index, null });
            }

            public void Remove (int index)
            {
                removeCustomSize.Invoke(Group, new object[] { index });
            }
        }
    }
}
