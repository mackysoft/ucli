using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Represents the <c>compile.diagnostic</c> stream payload. </summary>
public sealed record CompileDiagnosticEntry
{
    /// <summary> Initializes one compile diagnostic entry for a non-empty run identifier. </summary>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="RunId" /> is empty. </exception>
    [JsonConstructor]
    public CompileDiagnosticEntry (
        Guid RunId,
        CompileRefreshOrigin RefreshOrigin,
        IpcPrimaryDiagnostic? PrimaryDiagnostic)
    {
        if (RunId == Guid.Empty)
        {
            throw new ArgumentException("Run id must not be empty.", nameof(RunId));
        }
        if (!ContractLiteralCodec.IsDefined(RefreshOrigin))
        {
            throw new ArgumentOutOfRangeException(nameof(RefreshOrigin), RefreshOrigin, "Compile refresh origin must be defined.");
        }

        this.RunId = RunId;
        this.RefreshOrigin = RefreshOrigin;
        this.PrimaryDiagnostic = PrimaryDiagnostic;
    }

    public Guid RunId { get; }

    public CompileRefreshOrigin RefreshOrigin { get; }

    public IpcPrimaryDiagnostic? PrimaryDiagnostic { get; }
}
