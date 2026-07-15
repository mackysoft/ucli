using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Hosting.Cli.Common.Streaming;
using MackySoft.Ucli.Infrastructure.Text;

namespace MackySoft.Ucli.Hosting.Cli.Assurance;

/// <summary> Projects verify progress payloads into human-readable text entries. </summary>
internal sealed class VerifyProgressTextProjector : ICliCommandProgressTextProjector
{
    /// <inheritdoc />
    public bool TryCreateTextEntry<TPayload> (
        string eventName,
        TPayload payload,
        out string text)
        where TPayload : notnull
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
        var required = entry.Required ? "true" : "false";
        var kind = ContractLiteralCodec.ToValue(entry.Kind);
        var length = checked(7 + kind.Length + 10 + required.Length + 1 + status.Length);
        return string.Create(
            length,
            (Kind: kind, Required: required, Status: status),
            static (destination, state) =>
            {
                var writer = new SpanTextWriter(destination);
                writer.Append("verify ");
                writer.Append(state.Kind);
                writer.Append(" required=");
                writer.Append(state.Required);
                writer.Append(' ');
                writer.Append(state.Status);
            });
    }

    private static string CreateDiagnosticTextLine (VerifyDiagnosticEntry entry)
    {
        var stepKind = entry.StepKind.HasValue
            ? ContractLiteralCodec.ToValue(entry.StepKind.Value)
            : string.Empty;
        var severity = ContractLiteralCodec.ToValue(entry.Severity);
        var hasStep = stepKind.Length != 0;
        var length = checked(
            17
            + (hasStep ? 6 + stepKind.Length : 0)
            + 1
            + severity.Length
            + 1
            + entry.Code.Length
            + 2
            + entry.Message.Length);

        return string.Create(
            length,
            (HasStep: hasStep, StepKind: stepKind, Severity: severity, entry.Code, entry.Message),
            static (destination, state) =>
            {
                var writer = new SpanTextWriter(destination);
                writer.Append("verify diagnostic");
                if (state.HasStep)
                {
                    writer.Append(" step=");
                    writer.Append(state.StepKind);
                }
                writer.Append(' ');
                writer.Append(state.Severity);
                writer.Append(' ');
                writer.Append(state.Code);
                writer.Append(": ");
                writer.Append(state.Message);
            });
    }
}
