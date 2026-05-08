using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("C# eval touched resource item.")]
public sealed record CsEvalTouchedResourceDeclaration
{
    [JsonConstructor]
    public CsEvalTouchedResourceDeclaration (
        string kind,
        string path)
    {
        Kind = kind;
        Path = path;
    }

    [UcliRequired]
    [UcliDescription("Touched resource kind literal.")]
    public string Kind { get; init; }

    [UcliRequired]
    [UcliDescription("Project-relative touched resource path.")]
    public string Path { get; init; }
}
