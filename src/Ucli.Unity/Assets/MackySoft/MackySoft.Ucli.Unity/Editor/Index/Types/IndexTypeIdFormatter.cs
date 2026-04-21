using System;

#nullable enable

namespace MackySoft.Ucli.Unity.Index
{
    /// <summary> Formats stable type identifiers used by index catalog contracts. </summary>
    internal static class IndexTypeIdFormatter
    {
        /// <summary> Formats one runtime type to stable type identifier text. </summary>
        /// <param name="type"> The runtime type. </param>
        /// <returns> The formatted type identifier text. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="type" /> is <see langword="null" />. </exception>
        public static string Format (Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            var assemblyName = type.Assembly.GetName().Name ?? "unknown";
            var fullName = type.FullName ?? type.Name;
            return $"{fullName}, {assemblyName}";
        }
    }
}