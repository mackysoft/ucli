using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Hosting.Cli.Common.Streaming;

namespace MackySoft.Ucli.Hosting.Cli.Assurance;

/// <summary> Projects verify progress payloads into human-readable text entries. </summary>
internal sealed class VerifyProgressTextProjector : ICliCommandProgressTextProjector
{
    /// <inheritdoc />
    public bool TryCreateTextEntry (
        string eventName,
        object payload,
        out string text)
    {
        switch (eventName, payload)
        {
            case (VerifyProgressEventNames.StepStarted, VerifyStepProgressEntry entry):
                text = CreateStepTextLine(entry, "started");
                return true;
            case (VerifyProgressEventNames.StepCompleted, VerifyStepProgressEntry entry):
                text = CreateStepTextLine(entry, "completed");
                return true;
            case (VerifyProgressEventNames.StepSkipped, VerifyStepProgressEntry entry):
                text = CreateStepTextLine(entry, "skipped");
                return true;
            case (VerifyProgressEventNames.Diagnostic, VerifyDiagnosticEntry entry):
                text = CreateDiagnosticTextLine(entry);
                return true;
            default:
                text = string.Empty;
                return false;
        }
    }

    private static string CreateStepTextLine (
        VerifyStepProgressEntry entry,
        string status)
    {
        return string.Concat(
            "verify ",
            entry.Kind,
            " required=",
            entry.Required ? "true" : "false",
            " ",
            status);
    }

    private static string CreateDiagnosticTextLine (VerifyDiagnosticEntry entry)
    {
        var step = string.IsNullOrWhiteSpace(entry.StepKind)
            ? string.Empty
            : string.Concat(" step=", entry.StepKind);
        return string.Concat(
            "verify diagnostic",
            step,
            " ",
            entry.Severity,
            " ",
            entry.Code,
            ": ",
            entry.Message);
    }
}
