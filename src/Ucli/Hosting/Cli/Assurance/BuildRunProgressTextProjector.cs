using MackySoft.Ucli.Contracts.Text;
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
            case (_, BuildProgressEntry entry):
                text = CreateProgressText(entry);
                return true;
            case (BuildRunProgressEventNames.LogEntry, BuildLogEntry entry):
                text = CreateLogText(entry);
                return true;
            case (BuildRunProgressEventNames.Diagnostic, BuildDiagnosticEntry entry):
                text = CreateDiagnosticText(entry);
                return true;
        }

        text = CliProgressTextFormatter.CreateDelimitedEntry(eventName, " ", payload);
        return true;
    }

    private static string CreateProgressText (BuildProgressEntry entry)
    {
        const string Prefix = "build runId=";
        const string PhaseLabel = " phase=";
        const string RunnerKindLabel = " runnerKind=";
        const string RunnerStatusLabel = " runnerStatus=";
        const string VerdictLabel = " verdict=";

        var phase = ContractLiteralCodec.ToValue(entry.Phase);
        var runnerKind = entry.RunnerKind.HasValue
            ? ContractLiteralCodec.ToValue(entry.RunnerKind.Value)
            : null;
        var runnerStatus = entry.RunnerStatus.HasValue
            ? ContractLiteralCodec.ToValue(entry.RunnerStatus.Value)
            : null;
        var verdict = entry.Verdict.HasValue
            ? ContractLiteralCodec.ToValue(entry.Verdict.Value)
            : null;
        var length = checked(
            Prefix.Length
            + SpanTextLength.GuidDLength
            + PhaseLabel.Length
            + phase.Length
            + RunnerKindLabel.Length
            + GetNullableLength(runnerKind)
            + RunnerStatusLabel.Length
            + GetNullableLength(runnerStatus)
            + VerdictLabel.Length
            + GetNullableLength(verdict));

        return string.Create(
            length,
            (entry.RunId, Phase: phase, RunnerKind: runnerKind, RunnerStatus: runnerStatus, Verdict: verdict),
            static (destination, state) =>
            {
                var writer = new SpanTextWriter(destination);
                writer.Append(Prefix);
                writer.Append(state.RunId);
                writer.Append(PhaseLabel);
                writer.Append(state.Phase);
                writer.Append(RunnerKindLabel);
                AppendNullable(ref writer, state.RunnerKind);
                writer.Append(RunnerStatusLabel);
                AppendNullable(ref writer, state.RunnerStatus);
                writer.Append(VerdictLabel);
                AppendNullable(ref writer, state.Verdict);
            });
    }

    private static string CreateLogText (BuildLogEntry entry)
    {
        const string Prefix = "build log runId=";
        const string LevelLabel = " level=";
        const string SourceLabel = " source=";
        const string MessageLabel = " message=";

        var level = ContractLiteralCodec.ToValue(entry.Level);
        var source = ContractLiteralCodec.ToValue(entry.Source);
        var length = checked(
            Prefix.Length
            + SpanTextLength.GuidDLength
            + LevelLabel.Length
            + level.Length
            + SourceLabel.Length
            + source.Length
            + MessageLabel.Length
            + entry.Message.Length);

        return string.Create(
            length,
            (entry.RunId, Level: level, Source: source, entry.Message),
            static (destination, state) =>
            {
                var writer = new SpanTextWriter(destination);
                writer.Append(Prefix);
                writer.Append(state.RunId);
                writer.Append(LevelLabel);
                writer.Append(state.Level);
                writer.Append(SourceLabel);
                writer.Append(state.Source);
                writer.Append(MessageLabel);
                writer.Append(state.Message);
            });
    }

    private static string CreateDiagnosticText (BuildDiagnosticEntry entry)
    {
        const string Prefix = "build diagnostic runId=";
        const string PhaseLabel = " phase=";
        const string SeverityLabel = " severity=";
        const string CodeLabel = " code=";
        const string MessageLabel = " message=";

        var phase = ContractLiteralCodec.ToValue(entry.Phase);
        var severity = ContractLiteralCodec.ToValue(entry.Severity);
        var code = entry.Code.Value;
        var length = checked(
            Prefix.Length
            + SpanTextLength.GuidDLength
            + PhaseLabel.Length
            + phase.Length
            + SeverityLabel.Length
            + severity.Length
            + CodeLabel.Length
            + code.Length
            + MessageLabel.Length
            + entry.Message.Length);

        return string.Create(
            length,
            (entry.RunId, Phase: phase, Severity: severity, Code: code, entry.Message),
            static (destination, state) =>
            {
                var writer = new SpanTextWriter(destination);
                writer.Append(Prefix);
                writer.Append(state.RunId);
                writer.Append(PhaseLabel);
                writer.Append(state.Phase);
                writer.Append(SeverityLabel);
                writer.Append(state.Severity);
                writer.Append(CodeLabel);
                writer.Append(state.Code);
                writer.Append(MessageLabel);
                writer.Append(state.Message);
            });
    }

    private static int GetNullableLength (string? value)
    {
        return value?.Length ?? 4;
    }

    private static void AppendNullable (
        ref SpanTextWriter writer,
        string? value)
    {
        writer.Append(value ?? "null");
    }
}
