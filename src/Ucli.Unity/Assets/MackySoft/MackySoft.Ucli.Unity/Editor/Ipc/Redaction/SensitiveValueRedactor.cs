using System;
using System.Collections.Generic;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Applies deterministic redaction for sensitive values flowing through IPC-facing artifacts. </summary>
    internal static class SensitiveValueRedactor
    {
        public const string Replacement = "[ucli redacted environment value]";

        public static string[] CreateOrderedValues (IEnumerable<string>? values)
        {
            if (values == null)
            {
                return Array.Empty<string>();
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var orderedValues = new List<string>();
            foreach (var value in values)
            {
                if (!string.IsNullOrEmpty(value) && seen.Add(value))
                {
                    orderedValues.Add(value);
                }
            }

            orderedValues.Sort(static (left, right) => right.Length.CompareTo(left.Length));
            return orderedValues.ToArray();
        }

        public static string Redact (
            string value,
            IReadOnlyList<string> orderedValues)
        {
            if (orderedValues == null)
            {
                throw new ArgumentNullException(nameof(orderedValues));
            }

            if (orderedValues.Count == 0 || string.IsNullOrEmpty(value))
            {
                return value;
            }

            var redacted = value;
            for (var i = 0; i < orderedValues.Count; i++)
            {
                redacted = redacted.Replace(orderedValues[i], Replacement);
            }

            return redacted;
        }

        public static string? RedactOrNull (
            string? value,
            IReadOnlyList<string> orderedValues)
        {
            return value == null ? null : Redact(value, orderedValues);
        }
    }
}
