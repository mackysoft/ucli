using System;
using System.Collections.Generic;
using System.Linq;

namespace MackySoft.Ucli.Unity.ScreenshotCapture
{
    /// <summary> Plans fail-closed removal of request-owned temporary GameView resolutions. </summary>
    internal static class UnityScreenshotResolutionOrphanCleanupPlanner
    {
        private const string FixedResolutionKind = "FixedResolution";

        /// <summary> Validates every candidate before authorizing any removal. </summary>
        public static CleanupPlan CreatePlan (
            IReadOnlyList<UnityScreenshotResolutionLeaseRegistry.OwnedResolution> ownedResolutions,
            string currentGroupType,
            int? selectedIndex,
            IReadOnlyList<GroupEntry> groupEntries)
        {
            if (ownedResolutions == null)
            {
                throw new ArgumentNullException(nameof(ownedResolutions));
            }

            if (groupEntries == null)
            {
                throw new ArgumentNullException(nameof(groupEntries));
            }

            if (string.IsNullOrWhiteSpace(currentGroupType))
            {
                return CleanupPlan.Failure("Current GameView size group type is unavailable.");
            }

            var prefixedEntries = groupEntries
                .Where(entry => entry.BaseText?.StartsWith(
                    UnityScreenshotResolutionLeaseRegistry.LabelPrefix,
                    StringComparison.Ordinal) == true)
                .ToArray();
            if (ownedResolutions.Count == 0)
            {
                return prefixedEntries.Length == 0
                    ? CleanupPlan.Success(Array.Empty<int>(), Array.Empty<string>())
                    : CleanupPlan.Failure(
                        "A temporary-looking GameView resolution has no uCLI ownership marker; no entry was removed.");
            }

            if (ownedResolutions.Any(owned => owned == null
                    || !UnityScreenshotResolutionLeaseRegistry.IsOwnedLabelSyntax(owned.Label))
                || ownedResolutions
                    .GroupBy(owned => owned.Label, StringComparer.Ordinal)
                    .Any(group => group.Count() != 1))
            {
                return CleanupPlan.Failure(
                    "Temporary GameView resolution ownership markers are invalid or ambiguous; no entry was removed.");
            }

            if (ownedResolutions.Any(owned => !string.Equals(
                owned.GroupType,
                currentGroupType,
                StringComparison.Ordinal)))
            {
                return CleanupPlan.Failure(
                    "A temporary GameView resolution belongs to a non-current size group; no group was modified.");
            }

            var ownedByLabel = ownedResolutions.ToDictionary(
                owned => owned.Label,
                StringComparer.Ordinal);
            if (prefixedEntries.Any(entry =>
                !UnityScreenshotResolutionLeaseRegistry.IsOwnedLabelSyntax(entry.BaseText)
                || !ownedByLabel.ContainsKey(entry.BaseText)))
            {
                return CleanupPlan.Failure(
                    "A temporary-looking GameView resolution could not be attributed to uCLI; no entry was removed.");
            }

            var removalIndices = new List<int>();
            foreach (var owned in ownedResolutions)
            {
                var matches = prefixedEntries
                    .Where(entry => string.Equals(entry.BaseText, owned.Label, StringComparison.Ordinal))
                    .ToArray();
                if (matches.Length == 0)
                {
                    continue;
                }

                if (matches.Length != 1)
                {
                    return CleanupPlan.Failure(
                        $"Temporary GameView resolution ownership is ambiguous: {owned.Label}");
                }

                var entry = matches[0];
                if (!entry.IsCustom
                    || !string.Equals(entry.SizeType, FixedResolutionKind, StringComparison.Ordinal)
                    || entry.Width != owned.Width
                    || entry.Height != owned.Height)
                {
                    return CleanupPlan.Failure(
                        $"Temporary GameView resolution does not match its ownership marker: {owned.Label}");
                }

                if (selectedIndex == entry.Index)
                {
                    return CleanupPlan.Failure(
                        "A request-owned temporary GameView resolution is still selected; cleanup did not change selection.");
                }

                if (selectedIndex.HasValue && entry.Index < selectedIndex.Value)
                {
                    return CleanupPlan.Failure(
                        "Removing a temporary GameView resolution would shift the current selection; no entry was removed.");
                }

                removalIndices.Add(entry.Index);
            }

            if (removalIndices.Count != prefixedEntries.Length)
            {
                return CleanupPlan.Failure(
                    "Not every temporary-looking GameView resolution was proven request-owned; no entry was removed.");
            }

            if (removalIndices.Count != 0 && !selectedIndex.HasValue)
            {
                return CleanupPlan.Failure(
                    "The current GameView selection is unavailable; no temporary entry was removed.");
            }

            if (removalIndices.Distinct().Count() != removalIndices.Count)
            {
                return CleanupPlan.Failure(
                    "Temporary GameView resolution indices are ambiguous; no entry was removed.");
            }

            return CleanupPlan.Success(
                removalIndices.OrderByDescending(index => index).ToArray(),
                ownedResolutions.Select(owned => owned.Label).ToArray());
        }

        /// <summary> Describes one GameView size-group entry used for ownership validation. </summary>
        internal sealed record GroupEntry (
            int Index,
            string BaseText,
            string SizeType,
            int Width,
            int Height,
            bool IsCustom);

        /// <summary> Represents authorized removals or one fail-closed diagnostic. </summary>
        internal sealed record CleanupPlan (
            IReadOnlyList<int> RemovalIndices,
            IReadOnlyList<string> RegistryLabelsToClear,
            string ErrorMessage)
        {
            public bool IsSuccess => ErrorMessage == null;

            public static CleanupPlan Success (
                IReadOnlyList<int> removalIndices,
                IReadOnlyList<string> registryLabelsToClear)
            {
                return new CleanupPlan(removalIndices, registryLabelsToClear, ErrorMessage: null);
            }

            public static CleanupPlan Failure (string errorMessage)
            {
                return new CleanupPlan(
                    Array.Empty<int>(),
                    Array.Empty<string>(),
                    errorMessage);
            }
        }
    }
}
