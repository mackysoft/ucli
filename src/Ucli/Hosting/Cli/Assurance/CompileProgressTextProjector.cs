using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Hosting.Cli.Common.Streaming;
using MackySoft.Ucli.Infrastructure.Text;

namespace MackySoft.Ucli.Hosting.Cli.Assurance;

/// <summary> Projects compile progress payloads into human-readable text entries. </summary>
internal sealed class CompileProgressTextProjector : ICliCommandProgressTextProjector
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
            case (CompileProgressEventNames.Started, CompileStartedEntry entry):
                text = CreateStartedText(entry);
                return true;
            case (CompileProgressEventNames.RefreshStarted, CompileRefreshStartedEntry entry):
                text = CreateRefreshStartedText(entry);
                return true;
            case (CompileProgressEventNames.Recovered, CompileRecoveredEntry entry):
                text = CreateRecoveredText(entry);
                return true;
            case (CompileProgressEventNames.Diagnostic, CompileDiagnosticEntry entry):
                text = CreateDiagnosticText(entry);
                return true;
            case (CompileProgressEventNames.Completed, CompileCompletedEntry entry):
                text = CreateCompletedText(entry);
                return true;
            default:
                text = CliProgressTextFormatter.CreateDelimitedEntry(eventName, " ", payload);
                return true;
        }
    }

    private static string CreateStartedText (CompileStartedEntry entry)
    {
        const string Prefix = "compile runId=";
        const string RequestedModeLabel = " requestedMode=";
        const string ResolvedModeLabel = " resolvedMode=";
        const string SessionKindLabel = " sessionKind=";
        const string TimeoutLabel = " timeoutMs=";
        const string Status = " started";

        var timeoutLength = SpanTextLength.GetInvariantInt64Length(entry.TimeoutMilliseconds);
        var length = checked(Prefix.Length
            + entry.RunId.Length
            + RequestedModeLabel.Length
            + entry.RequestedMode.Length
            + ResolvedModeLabel.Length
            + entry.ResolvedMode.Length
            + SessionKindLabel.Length
            + entry.SessionKind.Length
            + TimeoutLabel.Length
            + timeoutLength
            + Status.Length);
        return string.Create(
            length,
            entry,
            static (destination, state) =>
            {
                var writer = new SpanTextWriter(destination);
                writer.Append(Prefix);
                writer.Append(state.RunId);
                writer.Append(RequestedModeLabel);
                writer.Append(state.RequestedMode);
                writer.Append(ResolvedModeLabel);
                writer.Append(state.ResolvedMode);
                writer.Append(SessionKindLabel);
                writer.Append(state.SessionKind);
                writer.Append(TimeoutLabel);
                writer.AppendInvariant(state.TimeoutMilliseconds);
                writer.Append(Status);
            });
    }

    private static string CreateRefreshStartedText (CompileRefreshStartedEntry entry)
    {
        const string Prefix = "compile refresh runId=";
        const string OriginLabel = " refreshOrigin=";
        const string SourceLabel = " observationSource=";
        const string Status = " started";

        var length = checked(Prefix.Length
            + entry.RunId.Length
            + OriginLabel.Length
            + entry.RefreshOrigin.Length
            + SourceLabel.Length
            + entry.ObservationSource.Length
            + Status.Length);
        return string.Create(
            length,
            entry,
            static (destination, state) =>
            {
                var writer = new SpanTextWriter(destination);
                writer.Append(Prefix);
                writer.Append(state.RunId);
                writer.Append(OriginLabel);
                writer.Append(state.RefreshOrigin);
                writer.Append(SourceLabel);
                writer.Append(state.ObservationSource);
                writer.Append(Status);
            });
    }

    private static string CreateRecoveredText (CompileRecoveredEntry entry)
    {
        const string Prefix = "compile recovery runId=";
        const string PollsLabel = " pollAttempts=";
        const string SummaryLabel = " summaryJsonPath=";
        const string Status = " recovered";

        var pollAttemptsLength = SpanTextLength.GetInvariantInt64Length(entry.PollAttempts);
        var length = checked(Prefix.Length
            + entry.RunId.Length
            + PollsLabel.Length
            + pollAttemptsLength
            + SummaryLabel.Length
            + entry.SummaryJsonPath.Length
            + Status.Length);
        return string.Create(
            length,
            entry,
            static (destination, state) =>
            {
                var writer = new SpanTextWriter(destination);
                writer.Append(Prefix);
                writer.Append(state.RunId);
                writer.Append(PollsLabel);
                writer.AppendInvariant(state.PollAttempts);
                writer.Append(SummaryLabel);
                writer.Append(state.SummaryJsonPath);
                writer.Append(Status);
            });
    }

    private static string CreateDiagnosticText (CompileDiagnosticEntry entry)
    {
        const string Prefix = "compile diagnostic runId=";
        const string OriginLabel = " refreshOrigin=";
        const string Separator = ": ";

        var diagnostic = entry.PrimaryDiagnostic;
        if (diagnostic is null)
        {
            var length = checked(Prefix.Length + entry.RunId.Length + OriginLabel.Length + entry.RefreshOrigin.Length);
            return string.Create(
                length,
                entry,
                static (destination, state) =>
                {
                    var writer = new SpanTextWriter(destination);
                    writer.Append(Prefix);
                    writer.Append(state.RunId);
                    writer.Append(OriginLabel);
                    writer.Append(state.RefreshOrigin);
                });
        }

        var code = string.IsNullOrWhiteSpace(diagnostic.Code) ? "compiler" : diagnostic.Code;
        var kind = string.IsNullOrWhiteSpace(diagnostic.Kind) ? "compiler" : diagnostic.Kind;
        var message = string.IsNullOrWhiteSpace(diagnostic.Message) ? "diagnostics-read summary created" : diagnostic.Message;
        var diagnosticLength = checked(
            Prefix.Length
            + entry.RunId.Length
            + OriginLabel.Length
            + entry.RefreshOrigin.Length
            + 1
            + kind.Length
            + 1
            + code.Length
            + Separator.Length
            + message.Length);
        return string.Create(
            diagnosticLength,
            (entry.RunId, entry.RefreshOrigin, Kind: kind, Code: code, Message: message),
            static (destination, state) =>
            {
                var writer = new SpanTextWriter(destination);
                writer.Append(Prefix);
                writer.Append(state.RunId);
                writer.Append(OriginLabel);
                writer.Append(state.RefreshOrigin);
                writer.Append(' ');
                writer.Append(state.Kind);
                writer.Append(' ');
                writer.Append(state.Code);
                writer.Append(Separator);
                writer.Append(state.Message);
            });
    }

    private static string CreateCompletedText (CompileCompletedEntry entry)
    {
        const string Prefix = "compile runId=";
        const string VerdictLabel = " verdict=";
        const string ErrorsLabel = " errorCount=";
        const string WarningsLabel = " warningCount=";
        const string Status = " completed";

        var errorCountLength = SpanTextLength.GetInvariantInt64Length(entry.ErrorCount);
        var warningCountLength = SpanTextLength.GetInvariantInt64Length(entry.WarningCount);
        var length = checked(Prefix.Length
            + entry.RunId.Length
            + VerdictLabel.Length
            + entry.Verdict.Length
            + ErrorsLabel.Length
            + errorCountLength
            + WarningsLabel.Length
            + warningCountLength
            + Status.Length);
        return string.Create(
            length,
            entry,
            static (destination, state) =>
            {
                var writer = new SpanTextWriter(destination);
                writer.Append(Prefix);
                writer.Append(state.RunId);
                writer.Append(VerdictLabel);
                writer.Append(state.Verdict);
                writer.Append(ErrorsLabel);
                writer.AppendInvariant(state.ErrorCount);
                writer.Append(WarningsLabel);
                writer.AppendInvariant(state.WarningCount);
                writer.Append(Status);
            });
    }

}
