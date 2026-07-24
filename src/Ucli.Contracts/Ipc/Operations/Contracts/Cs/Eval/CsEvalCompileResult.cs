using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("C# eval compile result.")]
public sealed record CsEvalCompileResult
{
    [JsonConstructor]
    public CsEvalCompileResult (
        CsEvalCompileStatus status,
        IReadOnlyList<CsEvalDiagnostic> diagnostics)
    {
        if (!TextVocabulary.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "C# eval compile status must be specified.");
        }

        Status = status;
        Diagnostics = ContractArgumentGuard.RequireItems(diagnostics, nameof(diagnostics));
    }

    [UcliRequired]
    [UcliDescription("Compile status.")]
    public CsEvalCompileStatus Status { get; }

    [UcliRequired]
    [UcliDescription("Compiler and entry point diagnostics.")]
    public IReadOnlyList<CsEvalDiagnostic> Diagnostics { get; }
}
