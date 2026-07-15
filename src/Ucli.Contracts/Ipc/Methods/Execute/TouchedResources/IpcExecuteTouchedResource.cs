using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one touched persistence-unit resource in a step result. </summary>
public sealed record IpcExecuteTouchedResource
{
    /// <summary> Initializes one touched persistence-unit resource. </summary>
    /// <param name="kind"> The touched resource kind. </param>
    /// <param name="path"> The project-relative path for the touched resource. </param>
    /// <param name="assetGuid"> The optional non-empty asset GUID. </param>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="kind" /> is not defined by the touched-resource contract. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="path" /> is not a normalized project-relative path or <paramref name="assetGuid" /> is <see cref="Guid.Empty" />. </exception>
    [JsonConstructor]
    public IpcExecuteTouchedResource (
        UcliTouchedResourceKind kind,
        string path,
        Guid? assetGuid)
    {
        if (!ContractLiteralCodec.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Touched-resource kind must be specified.");
        }

        if (assetGuid == Guid.Empty)
        {
            throw new ArgumentException("Touched-resource asset GUID must not be empty.", nameof(assetGuid));
        }

        if (!RelativePathContract.IsNormalized(path))
        {
            throw new ArgumentException("Touched-resource path must be a normalized project-relative path.", nameof(path));
        }

        Kind = kind;
        Path = path;
        AssetGuid = assetGuid;
    }

    /// <summary> Gets the touched resource kind. </summary>
    public UcliTouchedResourceKind Kind { get; }

    /// <summary> Gets the project-relative path for the touched resource. </summary>
    public string Path { get; }

    /// <summary> Gets the optional non-empty asset GUID. </summary>
    public Guid? AssetGuid { get; }
}
