using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Ipc.ContractReading;

/// <summary> Reads the <c>on</c> context object for one public edit step. </summary>
internal static class IpcEditStepContextReader
{
    public static bool TryRead (
        JsonElement stepElement,
        out IpcEditStepContract.EditContext context,
        out string errorMessage)
    {
        context = default!;
        if (!IpcEditStepContractReadHelpers.TryReadRequiredObject(
            stepElement,
            "on",
            "step.on",
            out var onElement,
            out errorMessage))
        {
            return false;
        }

        return IpcEditStepContextPropertyReader.TryRead(onElement, out context, out errorMessage);
    }
}
