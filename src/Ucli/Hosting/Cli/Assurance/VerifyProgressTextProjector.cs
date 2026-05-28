using MackySoft.Ucli.Contracts.Assurance;
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
        var length = checked(7 + entry.Kind.Length + 10 + required.Length + 1 + status.Length);
        return string.Create(
            length,
            (entry.Kind, Required: required, Status: status),
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
        var stepKind = entry.StepKind ?? string.Empty;
        var hasStep = !string.IsNullOrWhiteSpace(stepKind);
        var length = checked(
            17
            + (hasStep ? 6 + stepKind.Length : 0)
            + 1
            + entry.Severity.Length
            + 1
            + entry.Code.Length
            + 2
            + entry.Message.Length);

        return string.Create(
            length,
            (HasStep: hasStep, StepKind: stepKind, entry.Severity, entry.Code, entry.Message),
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
