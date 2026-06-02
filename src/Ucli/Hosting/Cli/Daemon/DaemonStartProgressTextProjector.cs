using MackySoft.Ucli.Contracts.Text;
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
    private const string EditorModePrefix = " editorMode=";
    private const string OwnerKindPrefix = " owner=";
    private const string CanShutdownProcessPrefix = " canShutdownProcess=";
    private const string ProcessIdPrefix = " pid=";
    private const string LaunchAttemptIdPrefix = " launchAttempt=";
    private const string StartupStatusPrefix = " startupStatus=";
    private const string StartupBlockingReasonPrefix = " startupBlockingReason=";
    private const string StartupPhasePrefix = " startupPhase=";
    private const string RetryDispositionPrefix = " retryDisposition=";
    private const string LifecycleStatePrefix = " lifecycleState=";
    private const string BlockingReasonPrefix = " blockingReason=";
    private const string CanAcceptExecutionRequestsPrefix = " canAcceptExecutionRequests=";
    private const string ErrorCodePrefix = " errorCode=";

    /// <inheritdoc />
    public bool TryCreateTextEntry<TPayload> (
        string eventName,
        TPayload payload,
        out string text)
        where TPayload : notnull
    {
        if (payload is DaemonStartProgressEntry entry)
        {
            text = CreateTextEntry(eventName, entry);
            return true;
        }

        if (payload is DaemonStartStartupObservationProgressEntry startupObservationEntry)
        {
            text = CreateStartupObservationTextEntry(eventName, startupObservationEntry);
            return true;
        }

        if (payload is DaemonStartLifecycleSnapshotProgressEntry lifecycleSnapshotEntry)
        {
            text = CreateLifecycleSnapshotTextEntry(eventName, lifecycleSnapshotEntry);
            return true;
        }

        text = CliProgressTextFormatter.CreateDelimitedEntry(eventName, " ", payload);
        return true;
    }

    private static string CreateTextEntry (
        string eventName,
        DaemonStartProgressEntry entry)
    {
        var progressEvent = ParseProgressEvent(eventName);
        var step = progressEvent switch
        {
            DaemonStartProgressEvent.Started => "workflow",
            DaemonStartProgressEvent.PluginVerificationStarted or DaemonStartProgressEvent.PluginVerificationCompleted => "pluginVerification",
            DaemonStartProgressEvent.SupervisorBootstrapStarted or DaemonStartProgressEvent.SupervisorBootstrapCompleted => "supervisorBootstrap",
            DaemonStartProgressEvent.EnsureRunningStarted or DaemonStartProgressEvent.EnsureRunningCompleted => "ensureRunning",
            DaemonStartProgressEvent.Completed => "workflow",
            _ => eventName,
        };
        var status = IsStartedEvent(progressEvent, eventName)
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

    private static string CreateStartupObservationTextEntry (
        string eventName,
        DaemonStartStartupObservationProgressEntry entry)
    {
        var progressEvent = ParseProgressEvent(eventName);
        var step = progressEvent switch
        {
            DaemonStartProgressEvent.Launching => "launch",
            DaemonStartProgressEvent.WaitingForEndpoint => "endpoint",
            DaemonStartProgressEvent.BlockerDetected => "blocker",
            DaemonStartProgressEvent.SessionRegistered => "session",
            DaemonStartProgressEvent.EndpointRegistered => "endpoint",
            _ => eventName,
        };
        var status = progressEvent switch
        {
            DaemonStartProgressEvent.Launching => "started",
            DaemonStartProgressEvent.WaitingForEndpoint => "waiting",
            DaemonStartProgressEvent.BlockerDetected => "detected",
            DaemonStartProgressEvent.SessionRegistered or DaemonStartProgressEvent.EndpointRegistered => "registered",
            _ => "observed",
        };
        var length = checked(
            Prefix.Length
            + step.Length
            + ProjectPrefix.Length
            + entry.ProjectFingerprint.Length
            + TimeoutPrefix.Length
            + GetInvariantInt32Length(entry.TimeoutMilliseconds)
            + GetOptionalLength(EditorModePrefix, entry.EditorMode)
            + GetOptionalLength(OwnerKindPrefix, entry.OwnerKind)
            + GetOptionalBoolLength(CanShutdownProcessPrefix, entry.CanShutdownProcess)
            + GetOptionalInt32Length(ProcessIdPrefix, entry.ProcessId)
            + GetOptionalLength(LaunchAttemptIdPrefix, entry.LaunchAttemptId)
            + GetOptionalLength(StartupStatusPrefix, entry.StartupStatus)
            + GetOptionalLength(StartupBlockingReasonPrefix, entry.StartupBlockingReason)
            + GetOptionalLength(StartupPhasePrefix, entry.StartupPhase)
            + GetOptionalLength(RetryDispositionPrefix, entry.RetryDisposition)
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
                AppendOptional(ref writer, EditorModePrefix, state.entry.EditorMode);
                AppendOptional(ref writer, OwnerKindPrefix, state.entry.OwnerKind);
                AppendOptional(ref writer, CanShutdownProcessPrefix, state.entry.CanShutdownProcess);
                AppendOptional(ref writer, ProcessIdPrefix, state.entry.ProcessId);
                AppendOptional(ref writer, LaunchAttemptIdPrefix, state.entry.LaunchAttemptId);
                AppendOptional(ref writer, StartupStatusPrefix, state.entry.StartupStatus);
                AppendOptional(ref writer, StartupBlockingReasonPrefix, state.entry.StartupBlockingReason);
                AppendOptional(ref writer, StartupPhasePrefix, state.entry.StartupPhase);
                AppendOptional(ref writer, RetryDispositionPrefix, state.entry.RetryDisposition);
                AppendOptional(ref writer, ErrorCodePrefix, state.entry.ErrorCode);
                writer.Append(' ');
                writer.Append(state.status);
            });
    }

    private static string CreateLifecycleSnapshotTextEntry (
        string eventName,
        DaemonStartLifecycleSnapshotProgressEntry entry)
    {
        var step = ParseProgressEvent(eventName) == DaemonStartProgressEvent.LifecycleObserved
            ? "lifecycle"
            : eventName;
        const string status = "observed";
        var length = checked(
            Prefix.Length
            + step.Length
            + ProjectPrefix.Length
            + entry.ProjectFingerprint.Length
            + TimeoutPrefix.Length
            + GetInvariantInt32Length(entry.TimeoutMilliseconds)
            + GetOptionalLength(EditorModePrefix, entry.EditorMode)
            + LifecycleStatePrefix.Length
            + entry.LifecycleState.Length
            + GetOptionalLength(BlockingReasonPrefix, entry.BlockingReason)
            + CanAcceptExecutionRequestsPrefix.Length
            + GetBoolLength(entry.CanAcceptExecutionRequests)
            + 1
            + status.Length);
        return string.Create(
            length,
            (entry, step),
            static (destination, state) =>
            {
                var writer = new SpanTextWriter(destination);
                writer.Append(Prefix);
                writer.Append(state.step);
                writer.Append(ProjectPrefix);
                writer.Append(state.entry.ProjectFingerprint);
                writer.Append(TimeoutPrefix);
                writer.AppendInvariant(state.entry.TimeoutMilliseconds);
                AppendOptional(ref writer, EditorModePrefix, state.entry.EditorMode);
                writer.Append(LifecycleStatePrefix);
                writer.Append(state.entry.LifecycleState);
                AppendOptional(ref writer, BlockingReasonPrefix, state.entry.BlockingReason);
                writer.Append(CanAcceptExecutionRequestsPrefix);
                AppendBool(ref writer, state.entry.CanAcceptExecutionRequests);
                writer.Append(' ');
                writer.Append(status);
            });
    }

    private static int GetOptionalLength (
        string prefix,
        string? value)
    {
        return value is null ? 0 : checked(prefix.Length + value.Length);
    }

    private static int GetOptionalInt32Length (
        string prefix,
        int? value)
    {
        return value.HasValue ? checked(prefix.Length + GetInvariantInt32Length(value.Value)) : 0;
    }

    private static int GetOptionalBoolLength (
        string prefix,
        bool? value)
    {
        return value.HasValue ? checked(prefix.Length + GetBoolLength(value.Value)) : 0;
    }

    private static int GetBoolLength (bool value)
    {
        return value ? 4 : 5;
    }

    private static DaemonStartProgressEvent? ParseProgressEvent (string eventName)
    {
        return ContractLiteralCodec.TryParse<DaemonStartProgressEvent>(eventName, out var progressEvent)
            ? progressEvent
            : null;
    }

    private static bool IsStartedEvent (
        DaemonStartProgressEvent? progressEvent,
        string eventName)
    {
        return progressEvent
            is DaemonStartProgressEvent.Started
            or DaemonStartProgressEvent.PluginVerificationStarted
            or DaemonStartProgressEvent.SupervisorBootstrapStarted
            or DaemonStartProgressEvent.EnsureRunningStarted
            || eventName.EndsWith(".started", StringComparison.Ordinal);
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

    private static void AppendOptional (
        ref SpanTextWriter writer,
        string prefix,
        int? value)
    {
        if (!value.HasValue)
        {
            return;
        }

        writer.Append(prefix);
        writer.AppendInvariant(value.Value);
    }

    private static void AppendOptional (
        ref SpanTextWriter writer,
        string prefix,
        bool? value)
    {
        if (!value.HasValue)
        {
            return;
        }

        writer.Append(prefix);
        AppendBool(ref writer, value.Value);
    }

    private static void AppendBool (
        ref SpanTextWriter writer,
        bool value)
    {
        writer.Append(value ? "true" : "false");
    }
}
