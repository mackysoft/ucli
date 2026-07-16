using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one diagnostic returned by a build runner terminal result. </summary>
/// <param name="Code"> The runner diagnostic code. </param>
/// <param name="Severity"> The diagnostic severity. </param>
/// <param name="Message"> The diagnostic message. </param>
public sealed record IpcBuildRunnerDiagnostic
{
    /// <summary> Initializes one build runner diagnostic. </summary>
    [JsonConstructor]
    public IpcBuildRunnerDiagnostic (
        string Code,
        UcliDiagnosticSeverity Severity,
        string Message)
    {
        if (!ContractLiteralCodec.IsDefined(Severity))
        {
            throw new ArgumentOutOfRangeException(nameof(Severity), Severity, "Build diagnostic severity must be specified.");
        }

        this.Code = ContractArgumentGuard.RequireValue(Code, nameof(Code));
        this.Severity = Severity;
        this.Message = ContractArgumentGuard.RequireValue(Message, nameof(Message));
    }

    public string Code { get; }

    public UcliDiagnosticSeverity Severity { get; }

    public string Message { get; }
}
