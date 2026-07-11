using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;

namespace MackySoft.Ucli.Unity.ScreenshotCapture
{
    /// <summary> Persists request-owned temporary GameView resolution identities across domain reload. </summary>
    internal static class UnityScreenshotResolutionLeaseRegistry
    {
        internal const string LabelPrefix = "__ucli_screenshot_temp__";

        private const int LabelTokenLength = 16;

        private const string SessionStateKey =
            "MackySoft.Ucli.ScreenshotCapture.TemporaryGameViewResolutions";

        /// <summary> Creates one exact, fixed-length ownership label accepted by orphan cleanup. </summary>
        public static string CreateLabel ()
        {
            return LabelPrefix + Guid.NewGuid().ToString("N").Substring(0, LabelTokenLength);
        }

        /// <summary> Registers one request-owned temporary resolution before it is added to Unity state. </summary>
        public static void Register (OwnedResolution ownedResolution)
        {
            if (ownedResolution == null)
            {
                throw new ArgumentNullException(nameof(ownedResolution));
            }

            if (!IsOwnedLabelSyntax(ownedResolution.Label)
                || ownedResolution.Width < 10
                || ownedResolution.Height < 10
                || string.IsNullOrWhiteSpace(ownedResolution.GroupType))
            {
                throw new ArgumentException(
                    "Temporary GameView resolution ownership metadata is invalid.",
                    nameof(ownedResolution));
            }

            if (!TryRead(out var entries, out var errorMessage))
            {
                throw new InvalidOperationException(errorMessage);
            }

            if (entries.Any(entry => string.Equals(
                entry.Label,
                ownedResolution.Label,
                StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    $"Temporary GameView resolution ownership label is already registered: {ownedResolution.Label}");
            }

            var next = entries.Concat(new[] { ownedResolution })
                .OrderBy(entry => entry.Label, StringComparer.Ordinal)
                .ToArray();
            SessionState.SetString(SessionStateKey, Serialize(next));
        }

        /// <summary> Removes one ownership marker after the corresponding entry is proven absent. </summary>
        public static bool TryUnregister (string label, out string errorMessage)
        {
            if (!IsOwnedLabelSyntax(label))
            {
                errorMessage = "Temporary GameView resolution ownership label is invalid.";
                return false;
            }

            if (!TryRead(out var entries, out errorMessage))
            {
                return false;
            }

            var next = entries
                .Where(entry => !string.Equals(entry.Label, label, StringComparison.Ordinal))
                .ToArray();
            SessionState.SetString(SessionStateKey, Serialize(next));
            errorMessage = null;
            return true;
        }

        /// <summary> Reads every valid ownership marker without repairing malformed state. </summary>
        public static bool TryRead (
            out IReadOnlyList<OwnedResolution> entries,
            out string errorMessage)
        {
            var serialized = SessionState.GetString(SessionStateKey, string.Empty);
            if (string.IsNullOrEmpty(serialized))
            {
                entries = Array.Empty<OwnedResolution>();
                errorMessage = null;
                return true;
            }

            var parsed = new List<OwnedResolution>();
            var labels = new HashSet<string>(StringComparer.Ordinal);
            var lines = serialized.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var fields = line.Split('\t');
                if (fields.Length != 5
                    || !IsOwnedLabelSyntax(fields[0])
                    || !int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out var width)
                    || !int.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out var height)
                    || !int.TryParse(fields[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var originalIndex)
                    || width < 10
                    || height < 10
                    || string.IsNullOrWhiteSpace(fields[3])
                    || fields[3].IndexOfAny(new[] { '\t', '\r', '\n' }) >= 0
                    || originalIndex < 0
                    || !labels.Add(fields[0]))
                {
                    entries = Array.Empty<OwnedResolution>();
                    errorMessage =
                        "Temporary GameView resolution ownership registry is malformed; no orphan entry was removed.";
                    return false;
                }

                parsed.Add(new OwnedResolution(
                    fields[0],
                    width,
                    height,
                    fields[3],
                    originalIndex));
            }

            entries = parsed;
            errorMessage = null;
            return true;
        }

        /// <summary> Determines whether a label exactly matches the uCLI ownership syntax. </summary>
        public static bool IsOwnedLabelSyntax (string label)
        {
            if (label == null
                || label.Length != LabelPrefix.Length + LabelTokenLength
                || !label.StartsWith(LabelPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            for (var index = LabelPrefix.Length; index < label.Length; index++)
            {
                var character = label[index];
                var isDigit = character >= '0' && character <= '9';
                var isLowercaseHex = character >= 'a' && character <= 'f';
                if (!isDigit && !isLowercaseHex)
                {
                    return false;
                }
            }

            return true;
        }

        internal static void ClearForTests ()
        {
            SessionState.EraseString(SessionStateKey);
        }

        private static string Serialize (IReadOnlyList<OwnedResolution> entries)
        {
            return string.Join(
                "\n",
                entries.Select(entry => string.Join(
                    "\t",
                    entry.Label,
                    entry.Width.ToString(CultureInfo.InvariantCulture),
                    entry.Height.ToString(CultureInfo.InvariantCulture),
                    entry.GroupType,
                    entry.OriginalIndex.ToString(CultureInfo.InvariantCulture))));
        }

        /// <summary> Describes one request-owned temporary resolution. </summary>
        internal sealed record OwnedResolution (
            string Label,
            int Width,
            int Height,
            string GroupType,
            int OriginalIndex);
    }
}
