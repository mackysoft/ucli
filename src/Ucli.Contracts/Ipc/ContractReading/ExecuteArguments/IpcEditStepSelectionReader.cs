using System.Text.Json;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc.ContractReading;

/// <summary> Reads the <c>select</c> object for one public edit step. </summary>
internal static class IpcEditStepSelectionReader
{
    public static bool TryRead (
        JsonElement stepElement,
        IpcEditStepContract.ContextKind contextKind,
        out IpcEditStepContract.EditSelection selection,
        out string errorMessage)
    {
        selection = default!;
        if (!IpcEditStepContractReadHelpers.TryReadRequiredObject(
            stepElement,
            "select",
            "step.select",
            out var selectElement,
            out errorMessage))
        {
            return false;
        }

        if (!TryReadCardinality(selectElement, out var cardinality, out errorMessage))
        {
            return false;
        }

        if (selectElement.TryGetProperty("from", out var fromElement))
        {
            return IpcEditStepFromSelectionReader.TryRead(selectElement, fromElement, contextKind, cardinality, out selection, out errorMessage);
        }

        return IpcEditStepDirectSelectionReader.TryRead(selectElement, contextKind, cardinality, out selection, out errorMessage);
    }

    private static bool TryReadCardinality (
        JsonElement selectElement,
        out IpcEditStepContract.CardinalityKind cardinality,
        out string errorMessage)
    {
        if (!IpcEditStepContractReadHelpers.TryReadRequiredString(
            selectElement,
            "cardinality",
            "step.select.cardinality",
            out var cardinalityLiteral,
            out errorMessage))
        {
            cardinality = default;
            return false;
        }

        if (!ContractLiteralCodec.TryParse(cardinalityLiteral!, out cardinality))
        {
            errorMessage = "Edit step property 'step.select.cardinality' must be one of 'one', 'first', 'all', or 'atMostOne'.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }
}
