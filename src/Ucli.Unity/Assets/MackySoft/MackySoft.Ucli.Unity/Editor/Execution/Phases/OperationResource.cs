using System;
using MackySoft.Ucli.Unity.Execution;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents one editable persistence-unit owner for temporary or live Unity objects. </summary>
    internal readonly struct OperationResource : IEquatable<OperationResource>
    {
        private const string ProjectSettingsRootPrefix = "ProjectSettings/";

        public OperationResource (
            OperationTouchKind kind,
            string path)
        {
            Kind = kind;
            Path = path;
        }

        /// <summary> Creates one persistent asset owner resource from a Unity asset path. </summary>
        /// <param name="assetPath"> The Unity asset path. </param>
        /// <returns> The normalized asset owner resource. </returns>
        public static OperationResource PersistentAsset (string assetPath)
        {
            var normalizedAssetPath = UnityAssetPathUtility.NormalizeAssetPath(assetPath);
            var kind = normalizedAssetPath.StartsWith(ProjectSettingsRootPrefix, StringComparison.Ordinal)
                ? OperationTouchKind.ProjectSettings
                : OperationTouchKind.Asset;
            return new OperationResource(kind, normalizedAssetPath);
        }

        public OperationTouchKind Kind { get; }

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
