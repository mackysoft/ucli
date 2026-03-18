using System.Text.RegularExpressions;

#nullable enable

namespace MackySoft.Ucli.Unity.Index
{
    /// <summary> Normalizes SerializedProperty paths to deterministic catalog contract paths. </summary>
    internal static class IndexSerializedPropertyPathNormalizer
    {
        private static readonly Regex ArrayIndexPattern = new(@"\.Array\.data\[\d+\]", RegexOptions.Compiled);

        /// <summary> Normalizes one SerializedProperty path. </summary>
        /// <param name="propertyPath"> The SerializedProperty path text. </param>
        /// <returns> The normalized path. </returns>
        /// <exception cref="System.ArgumentException"> Thrown when <paramref name="propertyPath" /> is <see langword="null" />, empty, or whitespace. </exception>
        public static string Normalize (string propertyPath)
        {
            if (string.IsNullOrWhiteSpace(propertyPath))
            {
                throw new System.ArgumentException("Property path must not be empty.", nameof(propertyPath));
            }

            return ArrayIndexPattern.Replace(propertyPath, ".Array.data[*]");
        }
    }
}