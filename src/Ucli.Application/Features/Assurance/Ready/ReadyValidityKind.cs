using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Ready;

/// <summary> Defines the finite validity scopes of ready claims. </summary>
internal enum ReadyValidityKind
{
    /// <summary> The claim remains valid for the observed reusable session. </summary>
    [UcliContractLiteral("sessionBound")]
    SessionBound = 1,

    /// <summary> The claim is valid only for the completed probe. </summary>
    [UcliContractLiteral("probeOnly")]
    ProbeOnly = 2,
}
