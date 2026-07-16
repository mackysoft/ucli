using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Compile.Payload;

/// <summary> Represents one evidence entry in a compile assurance claim. </summary>
internal sealed record CompileEvidenceOutput
{
    public CompileEvidenceOutput (
        CompileEvidenceKind Kind,
        string? EvidenceRef,
        object? Data)
    {
        if (!ContractLiteralCodec.IsDefined(Kind))
        {
            throw new ArgumentOutOfRangeException(nameof(Kind), Kind, "Unsupported compile evidence kind.");
        }

        this.Kind = Kind;
        this.EvidenceRef = EvidenceRef;
        this.Data = Data;
    }

    public CompileEvidenceKind Kind { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EvidenceRef { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Data { get; }
}
