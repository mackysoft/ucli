using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("C# eval touched resource item.")]
public sealed record CsEvalTouchedResourceDeclaration
{
    /// <summary> Initializes a touched resource declared by evaluated C# code. </summary>
    /// <param name="kind"> The specified touched-resource kind. </param>
    /// <param name="path"> The normalized project-relative resource path. </param>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="kind" /> is not defined by the touched-resource contract. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="path" /> is not a normalized project-relative path. </exception>
    [JsonConstructor]
    public CsEvalTouchedResourceDeclaration (
        UcliTouchedResourceKind kind,
        string path)
    {
        if (!ContractLiteralCodec.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Touched-resource kind must be specified.");
        }

        if (!RelativePathContract.IsNormalized(path))
        {
            throw new ArgumentException("Touched-resource path must be a normalized project-relative path.", nameof(path));
        }

        Kind = kind;
        Path = path;
    }

    [UcliRequired]
    [UcliDescription("Touched resource kind literal.")]
    public UcliTouchedResourceKind Kind { get; }

    [UcliRequired]
    [UcliDescription("Project-relative touched resource path.")]
    public string Path { get; }
}
