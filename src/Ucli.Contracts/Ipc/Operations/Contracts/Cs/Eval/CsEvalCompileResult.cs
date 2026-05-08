using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("C# eval compile result.")]
public sealed record CsEvalCompileResult
{
    [JsonConstructor]
    public CsEvalCompileResult (
        string status,
        IReadOnlyList<CsEvalDiagnostic> diagnostics)
    {
        Status = status;
        Diagnostics = diagnostics;
    }

    [UcliRequired]
    [UcliDescription("Compile status literal: succeeded or failed.")]
    public string Status { get; init; }

    [UcliRequired]
    [UcliDescription("Compiler and entry point diagnostics.")]
    public IReadOnlyList<CsEvalDiagnostic> Diagnostics { get; init; }
}
