using System;
using MackySoft.Text.Vocabularies;
using TextVocabulary = MackySoft.Text.Vocabularies.Vocabulary;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Text;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents one editable persistence-unit owner for temporary or live Unity objects. </summary>
    internal readonly struct OperationResource : IEquatable<OperationResource>
    {
        private const string ProjectSettingsRootPrefix = "ProjectSettings/";

        /// <summary> Initializes one resource owner from its kind and project-relative path. </summary>
        /// <param name="kind"> The resource kind. </param>
        /// <param name="path"> The project-relative path to normalize. </param>
        /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="kind" /> is not defined by the touched-resource contract. </exception>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="path" /> is not a valid project-relative path. </exception>
        public OperationResource (
            UcliTouchedResourceKind kind,
            string path)
        {
            if (!TextVocabulary.IsDefined(kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Operation resource kind is not supported.");
            }

            if (!RelativePathContract.TryNormalize(path, out var normalizedPath))
            {
                throw new ArgumentException("Operation resource path must be a valid project-relative path.", nameof(path));
            }

            Kind = kind;
            Path = normalizedPath;
        }

        /// <summary> Creates one persistent asset owner resource from a Unity asset path. </summary>
        /// <param name="assetPath"> The Unity asset path. </param>
        /// <returns> The normalized asset owner resource. </returns>
        public static OperationResource PersistentAsset (string assetPath)
        {
            if (!RelativePathContract.TryNormalize(assetPath, out var normalizedAssetPath))
            {
                throw new ArgumentException("Persistent asset path must be a valid project-relative path.", nameof(assetPath));
            }

            var kind = normalizedAssetPath.StartsWith(ProjectSettingsRootPrefix, StringComparison.Ordinal)
                ? UcliTouchedResourceKind.ProjectSettings
                : UcliTouchedResourceKind.Asset;
            return new OperationResource(kind, normalizedAssetPath);
        }

        public UcliTouchedResourceKind Kind { get; }

        public string Path { get; }

        public bool Equals (OperationResource other)
        {
            return Kind == other.Kind
                   && string.Equals(Path, other.Path, StringComparison.Ordinal);
        }

        public override bool Equals (object? obj)
        {
            return obj is OperationResource other && Equals(other);
        }

        public override int GetHashCode ()
        {
            unchecked
            {
                return ((int)Kind * 397) ^ StringComparer.Ordinal.GetHashCode(Path);
            }
        }
    }
}
