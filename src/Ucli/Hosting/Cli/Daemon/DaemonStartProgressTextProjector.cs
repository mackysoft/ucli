using MackySoft.Ucli.Hosting.Cli.Common.Streaming;

namespace MackySoft.Ucli.Hosting.Cli.Daemon;

/// <summary> Projects daemon-start progress payloads into human-readable text entries. </summary>
internal sealed class DaemonStartProgressTextProjector : ICliCommandProgressTextProjector
{
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
        var resultText = entry.Result is null ? string.Empty : $" result={entry.Result}";
        var startStatusText = entry.StartStatus is null ? string.Empty : $" startStatus={entry.StartStatus}";
        var daemonStatusText = entry.DaemonStatus is null ? string.Empty : $" daemonStatus={entry.DaemonStatus}";
        var errorCodeText = entry.ErrorCode is null ? string.Empty : $" errorCode={entry.ErrorCode}";
        return $"daemon start {step} project={entry.ProjectFingerprint} timeoutMs={entry.TimeoutMilliseconds}{resultText}{startStatusText}{daemonStatusText}{errorCodeText} {status}";
    }
}
