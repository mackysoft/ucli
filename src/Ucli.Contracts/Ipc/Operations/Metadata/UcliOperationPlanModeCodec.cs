using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Converts operation plan modes between enum and contract literals. </summary>
public static class UcliOperationPlanModeCodec
{
    private static readonly (UcliOperationPlanMode Value, string Literal)[] Mappings =
    {
        (UcliOperationPlanMode.ValidationOnly, UcliOperationPlanModeValues.ValidationOnly),
        (UcliOperationPlanMode.ObservesLiveUnity, UcliOperationPlanModeValues.ObservesLiveUnity),
        (UcliOperationPlanMode.MayCreatePreviewState, UcliOperationPlanModeValues.MayCreatePreviewState),
    };

    /// <summary> Converts one plan mode enum value to its contract literal. </summary>
    /// <param name="planMode"> The plan-mode enum value. </param>
    /// <returns> The contract literal value. </returns>
    public static string ToValue (UcliOperationPlanMode planMode)
    {
        return LiteralCodecUtilities.ToValue(
            planMode,
            Mappings,
            nameof(planMode),
            "Unsupported operation plan mode.");
    }
}
