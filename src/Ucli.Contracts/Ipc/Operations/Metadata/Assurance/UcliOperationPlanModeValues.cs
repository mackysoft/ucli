namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines literal values for operation plan behavior metadata. </summary>
public static class UcliOperationPlanModeValues
{
    /// <summary> Gets the value for plans that only validate args and static preconditions. </summary>
    public const string ValidationOnly = "validationOnly";

    /// <summary> Gets the value for plans that observe live Unity state without creating preview state. </summary>
    public const string ObservesLiveUnity = "observesLiveUnity";

    /// <summary> Gets the value for plans that can create request-scoped preview state. </summary>
    public const string MayCreatePreviewState = "mayCreatePreviewState";
}
