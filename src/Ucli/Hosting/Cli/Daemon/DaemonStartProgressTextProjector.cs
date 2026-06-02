using MackySoft.Ucli.Hosting.Cli.Common.Streaming;
using MackySoft.Ucli.Infrastructure.Text;

namespace MackySoft.Ucli.Hosting.Cli.Daemon;

/// <summary> Projects daemon-start progress payloads into human-readable text entries. </summary>
internal sealed class DaemonStartProgressTextProjector : ICliCommandProgressTextProjector
{
    private const string Prefix = "daemon start ";
    private const string ProjectPrefix = " project=";
    private const string TimeoutPrefix = " timeoutMs=";
    private const string ResultPrefix = " result=";
    private const string StartStatusPrefix = " startStatus=";
    private const string DaemonStatusPrefix = " daemonStatus=";
    private const string ErrorCodePrefix = " errorCode=";

    /// <inheritdoc />
    public bool TryCreateTextEntry<TPayload> (
        string eventName,
        TPayload payload,
        out string text)
        where TPayload : notnull
    {
        if (payload is not DaemonStartProgressEntry entry)
        {
            text = CliProgressTextFormatter.CreateDelimitedEntry(eventName, " ", payload);
            return true;
        }

        text = CreateTextEntry(eventName, entry);
        return true;
    }

    private static string CreateTextEntry (
        string eventName,
        DaemonStartProgressEntry entry)
    {
        var step = eventName switch
        {
            DaemonStartProgressEventNames.Started => "workflow",
            DaemonStartProgressEventNames.PluginVerificationStarted or DaemonStartProgressEventNames.PluginVerificationCompleted => "pluginVerification",
            DaemonStartProgressEventNames.SupervisorBootstrapStarted or DaemonStartProgressEventNames.SupervisorBootstrapCompleted => "supervisorBootstrap",
            DaemonStartProgressEventNames.EnsureRunningStarted or DaemonStartProgressEventNames.EnsureRunningCompleted => "ensureRunning",
            DaemonStartProgressEventNames.Completed => "workflow",
            _ => eventName,
        };
        var status = eventName.EndsWith(".started", StringComparison.Ordinal)
            || string.Equals(eventName, DaemonStartProgressEventNames.Started, StringComparison.Ordinal)
                ? "started"
                : "completed";
        var length = checked(
            Prefix.Length
            + step.Length
            + ProjectPrefix.Length
            + entry.ProjectFingerprint.Length
            + TimeoutPrefix.Length
            + GetInvariantInt32Length(entry.TimeoutMilliseconds)
            + GetOptionalLength(ResultPrefix, entry.Result)
            + GetOptionalLength(StartStatusPrefix, entry.StartStatus)
            + GetOptionalLength(DaemonStatusPrefix, entry.DaemonStatus)
            + GetOptionalLength(ErrorCodePrefix, entry.ErrorCode)
            + 1
            + status.Length);
        return string.Create(
            length,
            (entry, step, status),
            static (destination, state) =>
            {
                var writer = new SpanTextWriter(destination);
                writer.Append(Prefix);
                writer.Append(state.step);
                writer.Append(ProjectPrefix);
                writer.Append(state.entry.ProjectFingerprint);
                writer.Append(TimeoutPrefix);
                writer.AppendInvariant(state.entry.TimeoutMilliseconds);
                AppendOptional(ref writer, ResultPrefix, state.entry.Result);
                AppendOptional(ref writer, StartStatusPrefix, state.entry.StartStatus);
                AppendOptional(ref writer, DaemonStatusPrefix, state.entry.DaemonStatus);
                AppendOptional(ref writer, ErrorCodePrefix, state.entry.ErrorCode);
                writer.Append(' ');
                writer.Append(state.status);
            });
    }

    private static int GetOptionalLength (
        string prefix,
        string? value)
    {
        return value is null ? 0 : checked(prefix.Length + value.Length);
    }

    private static int GetInvariantInt32Length (int value)
    {
        if (value == 0)
        {
            return 1;
        }

        var length = value < 0 ? 1 : 0;
        var remaining = value < 0 ? -(long)value : value;
        while (remaining > 0)
        {
            remaining /= 10;
            length++;
        }

        return length;
    }

    private static void AppendOptional (
        ref SpanTextWriter writer,
        string prefix,
        string? value)
    {
        if (value == null)
        {
            return;
        }

        writer.Append(prefix);
        writer.Append(value);
    }
}
