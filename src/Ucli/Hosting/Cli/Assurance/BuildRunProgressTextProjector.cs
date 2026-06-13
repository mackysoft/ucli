using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Hosting.Cli.Common.Streaming;
using MackySoft.Ucli.Infrastructure.Text;

namespace MackySoft.Ucli.Hosting.Cli.Assurance;

/// <summary> Projects build run progress payloads into human-readable text entries. </summary>
internal sealed class BuildRunProgressTextProjector : ICliCommandProgressTextProjector
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
            case (BuildRunProgressEventNames.Started, BuildRunStartedEntry entry):
                text = CreateStartedText(entry);
                return true;
            case (BuildRunProgressEventNames.Completed, BuildRunCompletedEntry entry):
                text = CreateCompletedText(entry);
                return true;
        }

        text = CliProgressTextFormatter.CreateDelimitedEntry(eventName, " ", payload);
        return true;
    }

    private static string CreateStartedText (BuildRunStartedEntry entry)
    {
        const string Prefix = "build runId=";
        const string TargetLabel = " target=";
        const string RequestedModeLabel = " requestedMode=";
        const string ResolvedModeLabel = " resolvedMode=";
        const string SessionKindLabel = " sessionKind=";
        const string TimeoutLabel = " timeoutMs=";
        const string Status = " started";

        var timeoutLength = SpanTextLength.GetInvariantInt64Length(entry.TimeoutMilliseconds);
        var length = checked(Prefix.Length
            + entry.RunId.Length
            + TargetLabel.Length
            + entry.Target.Length
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
                writer.Append(TargetLabel);
                writer.Append(state.Target);
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

    private static string CreateCompletedText (BuildRunCompletedEntry entry)
    {
        const string Prefix = "build runId=";
        const string VerdictLabel = " verdict=";
        const string ResultLabel = " result=";
        const string CompletionLabel = " completionReason=";
        const string ErrorsLabel = " errorCount=";
        const string WarningsLabel = " warningCount=";
        const string Status = " completed";

        var errorCountLength = SpanTextLength.GetInvariantInt64Length(entry.ErrorCount);
        var warningCountLength = SpanTextLength.GetInvariantInt64Length(entry.WarningCount);
        var length = checked(Prefix.Length
            + entry.RunId.Length
            + VerdictLabel.Length
            + entry.Verdict.Length
            + ResultLabel.Length
            + entry.Result.Length
            + CompletionLabel.Length
            + entry.CompletionReason.Length
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
                writer.Append(ResultLabel);
                writer.Append(state.Result);
                writer.Append(CompletionLabel);
                writer.Append(state.CompletionReason);
                writer.Append(ErrorsLabel);
                writer.AppendInvariant(state.ErrorCount);
                writer.Append(WarningsLabel);
                writer.AppendInvariant(state.WarningCount);
                writer.Append(Status);
            });
    }
}
