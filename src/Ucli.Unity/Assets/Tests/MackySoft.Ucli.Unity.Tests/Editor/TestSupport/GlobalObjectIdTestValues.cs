using System;
using System.Globalization;
using UnityEditor;

namespace MackySoft.Ucli.Unity.Tests
{
    internal static class GlobalObjectIdTestValues
    {
        public static string CreateNonCanonicalIdentifierTypeText (string canonicalGlobalObjectId)
        {
            if (!GlobalObjectId.TryParse(canonicalGlobalObjectId, out var parsedCanonicalGlobalObjectId)
                || !string.Equals(parsedCanonicalGlobalObjectId.ToString(), canonicalGlobalObjectId, StringComparison.Ordinal))
            {
                throw new ArgumentException("GlobalObjectId must be canonical.", nameof(canonicalGlobalObjectId));
            }

            var separatorIndex = canonicalGlobalObjectId.IndexOf('-');
            var nonCanonicalGlobalObjectId = canonicalGlobalObjectId.Insert(separatorIndex + 1, "0");
            if (!GlobalObjectId.TryParse(nonCanonicalGlobalObjectId, out var parsedNonCanonicalGlobalObjectId)
                || !string.Equals(parsedNonCanonicalGlobalObjectId.ToString(), canonicalGlobalObjectId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Unity did not accept the equivalent non-canonical GlobalObjectId test value.");
            }

            return nonCanonicalGlobalObjectId;
        }

        public static string CreateWithIdentifierType (
            string canonicalGlobalObjectId,
            int identifierType)
        {
            if (!GlobalObjectId.TryParse(canonicalGlobalObjectId, out var parsedCanonicalGlobalObjectId)
                || !string.Equals(parsedCanonicalGlobalObjectId.ToString(), canonicalGlobalObjectId, StringComparison.Ordinal))
            {
                throw new ArgumentException("GlobalObjectId must be canonical.", nameof(canonicalGlobalObjectId));
            }

            var identifierTypeStartIndex = canonicalGlobalObjectId.IndexOf('-') + 1;
            var identifierTypeEndIndex = canonicalGlobalObjectId.IndexOf('-', identifierTypeStartIndex);
            var globalObjectId = canonicalGlobalObjectId.Substring(0, identifierTypeStartIndex)
                + identifierType.ToString(CultureInfo.InvariantCulture)
                + canonicalGlobalObjectId.Substring(identifierTypeEndIndex);
            if (!GlobalObjectId.TryParse(globalObjectId, out _))
            {
                throw new InvalidOperationException("Unity did not accept the requested GlobalObjectId identifier type test value.");
            }

            return globalObjectId;
        }
    }
}
