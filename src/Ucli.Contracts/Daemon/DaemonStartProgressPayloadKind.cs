using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Daemon;

/// <summary> Defines daemon-start progress payload-kind literals. </summary>
public enum DaemonStartProgressPayloadKind
{
    /// <summary> Endpoint-registration startup observation payload. </summary>
    [UcliContractLiteral("startupObservation")]
    StartupObservation = 0,

    /// <summary> Endpoint-registered lifecycle snapshot payload. </summary>
    [UcliContractLiteral("lifecycleSnapshot")]
    LifecycleSnapshot = 1,
}
