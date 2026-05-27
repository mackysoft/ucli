using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Operations;

/// <summary> Defines supported operation plan modes. </summary>
public enum UcliOperationPlanMode
{
    /// <summary> Plans that only validate args and static preconditions. </summary>
    [UcliContractLiteral("validationOnly")]
    ValidationOnly = 0,

    /// <summary> Plans that observe live Unity state without creating preview state. </summary>
    [UcliContractLiteral("observesLiveUnity")]
    ObservesLiveUnity = 1,

    /// <summary> Plans that can create request-scoped preview state. </summary>
    [UcliContractLiteral("mayCreatePreviewState")]
    MayCreatePreviewState = 2,
}
