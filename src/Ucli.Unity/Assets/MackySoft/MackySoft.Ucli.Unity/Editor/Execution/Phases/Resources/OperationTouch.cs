using System;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Text;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents one touched persistence-unit entry from operation execution. </summary>
    public sealed record OperationTouch
    {
        /// <summary> Initializes one valid touched-resource entry. </summary>
        /// <param name="kind"> The touched unit kind. </param>
        /// <param name="path"> The project-relative path to normalize. </param>
        /// <param name="assetGuid"> The optional non-empty asset GUID. </param>
        /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="kind" /> is not a supported touched-resource kind. </exception>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="path" /> is not a valid project-relative path or <paramref name="assetGuid" /> is <see cref="Guid.Empty" />. </exception>
        public OperationTouch (
            UcliTouchedResourceKind kind,
            string path,
            Guid? assetGuid)
        {
            if (!ContractLiteralCodec.IsDefined(kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Touched-resource kind is not supported.");
            }

            if (!RelativePathContract.TryNormalize(path, out var normalizedPath))
            {
                throw new ArgumentException("Touched-resource path must be a valid project-relative path.", nameof(path));
            }

            if (assetGuid == Guid.Empty)
            {
                throw new ArgumentException("Touched-resource asset GUID must not be empty.", nameof(assetGuid));
            }

            Kind = kind;
            Path = normalizedPath;
            AssetGuid = assetGuid;
        }

        /// <summary> Gets the touched unit kind. </summary>
        public UcliTouchedResourceKind Kind { get; }

        /// <summary> Gets the normalized project-relative path. </summary>
        public string Path { get; }

        /// <summary> Gets the optional non-empty asset GUID. </summary>
        public Guid? AssetGuid { get; }
    }
}
