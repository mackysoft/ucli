using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;

namespace MackySoft.Ucli.Unity.ScreenshotCapture
{
    /// <summary> Removes only fully attributed orphaned GameView resolutions from the current size group. </summary>
    internal sealed class UnityScreenshotResolutionOrphanCleaner : IUnityScreenshotResolutionOrphanCleaner
    {
        private const BindingFlags InstanceMembers =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private const BindingFlags StaticMembers =
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;

        private readonly Assembly unityEditorAssembly = typeof(EditorWindow).Assembly;

        /// <summary> Cleans stale request-owned entries without persisting or changing the current selection. </summary>
        public bool TryCleanup (out string errorMessage)
        {
            if (!UnityScreenshotResolutionLeaseRegistry.TryRead(
                out var ownedResolutions,
                out errorMessage))
            {
                return false;
            }

            try
            {
                if (!TryResolveContext(out var context, out errorMessage)
                    || !TrySnapshotGroup(context, out var before, out errorMessage))
                {
                    return false;
                }

                var plan = UnityScreenshotResolutionOrphanCleanupPlanner.CreatePlan(
                    ownedResolutions,
                    context.GroupTypeName,
                    context.SelectedIndex,
                    before.Select(entry => entry.Descriptor).ToArray());
                if (!plan.IsSuccess)
                {
                    errorMessage = plan.ErrorMessage;
                    return false;
                }

                if (!IsSameCurrentContext(context, out errorMessage))
                {
                    return false;
                }

                var unrelatedSizes = before
                    .Where(entry => !HasTemporaryPrefix(entry.Descriptor.BaseText))
                    .Select(entry => entry.Size)
                    .ToArray();
                foreach (var removalIndex in plan.RemovalIndices)
                {
                    var expected = before.Single(entry => entry.Descriptor.Index == removalIndex);
                    if (context.GetGameViewSize.Invoke(
                        context.Group,
                        new object[] { removalIndex }) is not object current
                        || !ReferenceEquals(current, expected.Size))
                    {
                        errorMessage =
                            "Temporary GameView resolution identity changed before cleanup; no further entry was removed.";
                        return false;
                    }

                    context.RemoveCustomSize.Invoke(context.Group, new object[] { removalIndex });
                }

                if (!IsSameCurrentContext(context, out errorMessage)
                    || !TrySnapshotGroup(context, out var after, out errorMessage))
                {
                    return false;
                }

                var remainingUnrelatedSizes = after
                    .Where(entry => !HasTemporaryPrefix(entry.Descriptor.BaseText))
                    .Select(entry => entry.Size)
                    .ToArray();
                if (after.Count != before.Count - plan.RemovalIndices.Count
                    || after.Any(entry => HasTemporaryPrefix(entry.Descriptor.BaseText))
                    || unrelatedSizes.Length != remainingUnrelatedSizes.Length
                    || unrelatedSizes.Where((size, index) =>
                        !ReferenceEquals(size, remainingUnrelatedSizes[index])).Any())
                {
                    errorMessage =
                        "GameView resolution cleanup could not prove that only request-owned entries were removed.";
                    return false;
                }

                foreach (var label in plan.RegistryLabelsToClear)
                {
                    if (!UnityScreenshotResolutionLeaseRegistry.TryUnregister(label, out errorMessage))
                    {
                        return false;
                    }
                }

                errorMessage = null;
                return true;
            }
            catch (Exception exception)
            {
                errorMessage =
                    $"Temporary GameView resolution cleanup failed. {UnwrapReflectionException(exception).Message}";
                return false;
            }
        }

        private bool TryResolveContext (
            out CleanupContext context,
            out string errorMessage)
        {
            context = null;
            var gameViewSizesType = unityEditorAssembly.GetType("UnityEditor.GameViewSizes");
            var playModeViewType = unityEditorAssembly.GetType("UnityEditor.PlayModeView");
            var gameViewType = unityEditorAssembly.GetType("UnityEditor.GameView");
            var instanceProperty = gameViewSizesType?.GetProperty("instance", StaticMembers);
            var currentGroupProperty = FindProperty(gameViewSizesType, "currentGroup");
            var currentGroupTypeProperty = FindProperty(gameViewSizesType, "currentGroupType");
            var getMainPlayModeView = playModeViewType?.GetMethod(
                "GetMainPlayModeView",
                StaticMembers,
                binder: null,
                Type.EmptyTypes,
                modifiers: null);
            var sizesInstance = instanceProperty?.GetValue(obj: null);
            var group = sizesInstance == null ? null : currentGroupProperty?.GetValue(sizesInstance);
            var groupTypeValue = sizesInstance == null
                ? null
                : currentGroupTypeProperty?.GetValue(sizesInstance);
            if (gameViewSizesType == null
                || playModeViewType == null
                || gameViewType == null
                || sizesInstance == null
                || group == null
                || groupTypeValue == null
                || currentGroupProperty == null
                || currentGroupTypeProperty == null
                || getMainPlayModeView == null)
            {
                errorMessage = "Unity GameView size-group cleanup adapter members are unavailable.";
                return false;
            }

            var groupType = group.GetType();
            var getTotalCount = FindMethod(groupType, "GetTotalCount", Type.EmptyTypes);
            var getGameViewSize = FindMethod(groupType, "GetGameViewSize", new[] { typeof(int) });
            var isCustomSize = FindMethod(groupType, "IsCustomSize", new[] { typeof(int) });
            var removeCustomSize = FindMethod(groupType, "RemoveCustomSize", new[] { typeof(int) });
            var mainView = getMainPlayModeView.Invoke(obj: null, parameters: null);
            int? selectedIndex = null;
            PropertyInfo selectedSizeIndexProperty = null;
            if (mainView != null)
            {
                if (mainView.GetType() == gameViewType)
                {
                    selectedSizeIndexProperty = FindProperty(gameViewType, "selectedSizeIndex");
                    if (selectedSizeIndexProperty?.GetValue(mainView) is not int value)
                    {
                        errorMessage = "Current GameView resolution selection is unavailable.";
                        return false;
                    }

                    selectedIndex = value;
                }
                else
                {
                    mainView = null;
                }
            }

            if (getTotalCount == null
                || getGameViewSize == null
                || isCustomSize == null
                || removeCustomSize == null)
            {
                errorMessage = "Unity GameView size-group cleanup methods are unavailable.";
                return false;
            }

            context = new CleanupContext(
                sizesInstance,
                currentGroupProperty,
                currentGroupTypeProperty,
                group,
                groupTypeValue.ToString(),
                mainView,
                selectedSizeIndexProperty,
                selectedIndex,
                getTotalCount,
                getGameViewSize,
                isCustomSize,
                removeCustomSize);
            errorMessage = null;
            return true;
        }

        private static bool TrySnapshotGroup (
            CleanupContext context,
            out IReadOnlyList<ResolvedGroupEntry> entries,
            out string errorMessage)
        {
            entries = Array.Empty<ResolvedGroupEntry>();
            if (context.GetTotalCount.Invoke(context.Group, parameters: null) is not int count
                || count < 0)
            {
                errorMessage = "Current GameView size-group count is unavailable.";
                return false;
            }

            var resolved = new List<ResolvedGroupEntry>(count);
            for (var index = 0; index < count; index++)
            {
                var size = context.GetGameViewSize.Invoke(context.Group, new object[] { index });
                if (size == null
                    || context.IsCustomSize.Invoke(
                        context.Group,
                        new object[] { index }) is not bool isCustom)
                {
                    errorMessage = "A GameView size-group entry could not be inspected.";
                    return false;
                }

                var sizeType = size.GetType();
                var baseTextProperty = FindProperty(sizeType, "baseText");
                var sizeTypeProperty = FindProperty(sizeType, "sizeType");
                var widthProperty = FindProperty(sizeType, "width");
                var heightProperty = FindProperty(sizeType, "height");
                var baseText = baseTextProperty?.GetValue(size) as string;
                var kind = sizeTypeProperty?.GetValue(size);
                if (baseText == null
                    || kind == null
                    || widthProperty?.GetValue(size) is not int width
                    || heightProperty?.GetValue(size) is not int height)
                {
                    errorMessage = "A GameView size-group entry descriptor is unavailable.";
                    return false;
                }

                resolved.Add(new ResolvedGroupEntry(
                    size,
                    new UnityScreenshotResolutionOrphanCleanupPlanner.GroupEntry(
                        index,
                        baseText,
                        kind.ToString(),
                        width,
                        height,
                        isCustom)));
            }

            entries = resolved;
            errorMessage = null;
            return true;
        }

        private static bool IsSameCurrentContext (
            CleanupContext context,
            out string errorMessage)
        {
            var currentGroup = context.CurrentGroupProperty.GetValue(context.SizesInstance);
            var currentGroupType = context.CurrentGroupTypeProperty.GetValue(context.SizesInstance)?.ToString();
            if (!ReferenceEquals(currentGroup, context.Group)
                || !string.Equals(currentGroupType, context.GroupTypeName, StringComparison.Ordinal))
            {
                errorMessage = "Current GameView size group changed during orphan cleanup.";
                return false;
            }

            if (context.MainView != null)
            {
                if (context.MainView is not EditorWindow editorWindow
                    || editorWindow == null
                    || context.SelectedSizeIndexProperty?.GetValue(context.MainView) is not int selectedIndex
                    || selectedIndex != context.SelectedIndex
                    || context.GetGameViewSize.Invoke(
                        context.Group,
                        new object[] { selectedIndex }) is not object selectedSize)
                {
                    errorMessage = "Current GameView resolution selection changed during orphan cleanup.";
                    return false;
                }

                if (context.SelectedSize == null)
                {
                    context.SelectedSize = selectedSize;
                }
                else if (!ReferenceEquals(context.SelectedSize, selectedSize))
                {
                    errorMessage = "Current GameView resolution identity changed during orphan cleanup.";
                    return false;
                }
            }

            errorMessage = null;
            return true;
        }

        private static bool HasTemporaryPrefix (string label)
        {
            return label?.StartsWith(
                UnityScreenshotResolutionLeaseRegistry.LabelPrefix,
                StringComparison.Ordinal) == true;
        }

        private static PropertyInfo FindProperty (Type type, string name)
        {
            while (type != null)
            {
                var property = type.GetProperty(name, InstanceMembers);
                if (property != null)
                {
                    return property;
                }

                type = type.BaseType;
            }

            return null;
        }

        private static MethodInfo FindMethod (
            Type type,
            string name,
            Type[] parameterTypes)
        {
            while (type != null)
            {
                var method = type.GetMethod(
                    name,
                    InstanceMembers,
                    binder: null,
                    parameterTypes,
                    modifiers: null);
                if (method != null)
                {
                    return method;
                }

                type = type.BaseType;
            }

            return null;
        }

        private static Exception UnwrapReflectionException (Exception exception)
        {
            return exception is TargetInvocationException { InnerException: not null } invocationException
                ? invocationException.InnerException
                : exception;
        }

        private sealed record ResolvedGroupEntry (
            object Size,
            UnityScreenshotResolutionOrphanCleanupPlanner.GroupEntry Descriptor);

        private sealed record CleanupContext (
            object SizesInstance,
            PropertyInfo CurrentGroupProperty,
            PropertyInfo CurrentGroupTypeProperty,
            object Group,
            string GroupTypeName,
            object MainView,
            PropertyInfo SelectedSizeIndexProperty,
            int? SelectedIndex,
            MethodInfo GetTotalCount,
            MethodInfo GetGameViewSize,
            MethodInfo IsCustomSize,
            MethodInfo RemoveCustomSize)
        {
            public object SelectedSize { get; set; }
        }
    }
}
