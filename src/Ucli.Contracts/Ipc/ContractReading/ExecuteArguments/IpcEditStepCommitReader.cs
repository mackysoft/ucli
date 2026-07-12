using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Ipc.ContractReading;

/// <summary> Reads the <c>commit</c> literal for one public edit step. </summary>
internal static class IpcEditStepCommitReader
{
    public static bool TryRead (
        JsonElement stepElement,
        out IpcEditStepContract.CommitKind commitKind,
        out string errorMessage)
    {
        commitKind = default;
        if (!IpcEditStepContractReadHelpers.TryReadRequiredString(
            stepElement,
            "commit",
            "step.commit",
            out var commitLiteral,
            out errorMessage))
        {
            return false;
        }

        if (!IpcCamelCaseEnumLiteralParser.TryParse(commitLiteral!, out commitKind))
        {
            errorMessage = "Edit step property 'step.commit' must be one of 'none', 'context', or 'project'.";
            return false;
        }

        return true;
    }
}
