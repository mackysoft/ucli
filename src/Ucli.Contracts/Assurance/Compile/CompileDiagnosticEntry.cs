using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Represents the <c>compile.diagnostic</c> stream payload. </summary>
public sealed record CompileDiagnosticEntry (
    string RunId,
    string RefreshOrigin,
    IpcPrimaryDiagnostic? PrimaryDiagnostic);
