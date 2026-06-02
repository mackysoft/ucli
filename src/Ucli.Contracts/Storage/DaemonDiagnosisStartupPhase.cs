using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Storage;

/// <summary> Defines normalized daemon diagnosis startup-phase literals. </summary>
public enum DaemonDiagnosisStartupPhase
{
    /// <summary> Unity script compilation blocked startup. </summary>
    [UcliContractLiteral("scriptCompilation")]
    ScriptCompilation = 0,

    /// <summary> Unity package resolution blocked startup. </summary>
    [UcliContractLiteral("packageResolution")]
    PackageResolution = 1,

    /// <summary> Unity Editor is waiting for user action. </summary>
    [UcliContractLiteral("userAction")]
    UserAction = 2,

    /// <summary> Unity Editor exited before bootstrap completed. </summary>
    [UcliContractLiteral("processExit")]
    ProcessExit = 3,

    /// <summary> Startup is waiting for GUI endpoint registration. </summary>
    [UcliContractLiteral("endpointRegistration")]
    EndpointRegistration = 4,
}
