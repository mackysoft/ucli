namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines supported operation plan modes. </summary>
public enum UcliOperationPlanMode
{
    /// <summary> Plans that only validate args and static preconditions. </summary>
    ValidationOnly = 0,

    /// <summary> Plans that observe live Unity state without creating preview state. </summary>
    ObservesLiveUnity = 1,

    /// <summary> Plans that can create request-scoped preview state. </summary>
    MayCreatePreviewState = 2,
}
