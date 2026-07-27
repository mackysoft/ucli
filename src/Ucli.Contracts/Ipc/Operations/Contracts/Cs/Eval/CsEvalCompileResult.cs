using System.Text.Json.Serialization;

using MackySoft.JsonSchema.Generation.Annotations;

namespace MackySoft.Ucli.Contracts.Ipc;

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

    [JsonInclude]
    [JsonRequired]
    [Description("Compile status.")]
    public CsEvalCompileStatus Status { get; private init; }

    [JsonInclude]
    [JsonRequired]
    [Description("Compiler and entry point diagnostics.")]
    public IReadOnlyList<CsEvalDiagnostic> Diagnostics { get; private init; }
}
