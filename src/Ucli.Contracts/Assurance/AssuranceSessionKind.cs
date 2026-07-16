using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Defines the finite session kinds emitted by assurance commands. </summary>
public enum AssuranceSessionKind
{
    /// <summary> A reusable daemon session handled the operation. </summary>
    [UcliContractLiteral("daemon")]
    Daemon = 1,

    /// <summary> A temporary one-shot session handled the probe. </summary>
    [UcliContractLiteral("transientProbe")]
    TransientProbe = 2,

    /// <summary> The result was determined from persisted artifacts without a Unity session. </summary>
    [UcliContractLiteral("artifactOnly")]
    ArtifactOnly = 3,
}
