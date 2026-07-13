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
        var progressEvent = ContractLiteralCodec.TryParse<DaemonStartProgressEvent>(eventName, out var parsedProgressEvent)
            ? parsedProgressEvent
            : (DaemonStartProgressEvent?)null;
        var step = progressEvent switch
        {
            DaemonStartProgressEvent.Started => "workflow",
            DaemonStartProgressEvent.PluginVerificationStarted or DaemonStartProgressEvent.PluginVerificationCompleted => "pluginVerification",
            DaemonStartProgressEvent.SupervisorBootstrapStarted or DaemonStartProgressEvent.SupervisorBootstrapCompleted => "supervisorBootstrap",
            DaemonStartProgressEvent.EnsureRunningStarted or DaemonStartProgressEvent.EnsureRunningCompleted => "ensureRunning",
            DaemonStartProgressEvent.Completed => "workflow",
            _ => eventName,
        };
        var status = IsStartedEvent(progressEvent)
                || (progressEvent is null && eventName.EndsWith(".started", StringComparison.Ordinal))
                ? "started"
                : "completed";
        var projectFingerprint = entry.ProjectFingerprint.ToString();
        var length = checked(
            Prefix.Length
            + step.Length
            + ProjectPrefix.Length
            + projectFingerprint.Length
            + TimeoutPrefix.Length
            + SpanTextLength.GetInvariantInt64Length(entry.TimeoutMilliseconds)
            + SpanTextLength.GetOptionalStringLength(ResultPrefix, entry.Result)
            + SpanTextLength.GetOptionalStringLength(StartStatusPrefix, entry.StartStatus)
            + SpanTextLength.GetOptionalStringLength(DaemonStatusPrefix, entry.DaemonStatus)
            + SpanTextLength.GetOptionalStringLength(ErrorCodePrefix, entry.ErrorCode)
            + 1
            + status.Length);
        return string.Create(
            length,
            (entry, step, status, projectFingerprint),
            static (destination, state) =>
            {
                var writer = new SpanTextWriter(destination);
                writer.Append(Prefix);
                writer.Append(state.step);
                writer.Append(ProjectPrefix);
                writer.Append(state.projectFingerprint);
                writer.Append(TimeoutPrefix);
                writer.AppendInvariant(state.entry.TimeoutMilliseconds);
                writer.AppendOptional(ResultPrefix, state.entry.Result);
                writer.AppendOptional(StartStatusPrefix, state.entry.StartStatus);
                writer.AppendOptional(DaemonStatusPrefix, state.entry.DaemonStatus);
                writer.AppendOptional(ErrorCodePrefix, state.entry.ErrorCode);
                writer.Append(' ');
                writer.Append(state.status);
            });
    }

    private static string CreateStartupObservationTextEntry (
        string eventName,
        DaemonStartStartupObservationProgressEntry entry)
    {
        var progressEvent = ContractLiteralCodec.TryParse<DaemonStartProgressEvent>(eventName, out var parsedProgressEvent)
            ? parsedProgressEvent
            : (DaemonStartProgressEvent?)null;
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
        var projectFingerprint = entry.ProjectFingerprint.ToString();
        var length = checked(
            Prefix.Length
            + step.Length
            + ProjectPrefix.Length
            + projectFingerprint.Length
            + TimeoutPrefix.Length
            + SpanTextLength.GetInvariantInt64Length(entry.TimeoutMilliseconds)
            + SpanTextLength.GetOptionalStringLength(EditorModePrefix, entry.EditorMode)
            + SpanTextLength.GetOptionalStringLength(OwnerKindPrefix, entry.OwnerKind)
            + SpanTextLength.GetOptionalBoolLength(CanShutdownProcessPrefix, entry.CanShutdownProcess)
            + SpanTextLength.GetOptionalInvariantInt64Length(ProcessIdPrefix, entry.ProcessId)
            + SpanTextLength.GetOptionalStringLength(LaunchAttemptIdPrefix, entry.LaunchAttemptId)
            + SpanTextLength.GetOptionalStringLength(StartupStatusPrefix, entry.StartupStatus)
            + SpanTextLength.GetOptionalStringLength(StartupBlockingReasonPrefix, entry.StartupBlockingReason)
            + SpanTextLength.GetOptionalStringLength(StartupPhasePrefix, entry.StartupPhase)
            + SpanTextLength.GetOptionalStringLength(RetryDispositionPrefix, entry.RetryDisposition)
            + SpanTextLength.GetOptionalStringLength(ErrorCodePrefix, entry.ErrorCode)
            + 1
            + status.Length);
        return string.Create(
            length,
            (entry, step, status, projectFingerprint),
            static (destination, state) =>
            {
                var writer = new SpanTextWriter(destination);
                writer.Append(Prefix);
                writer.Append(state.step);
                writer.Append(ProjectPrefix);
                writer.Append(state.projectFingerprint);
                writer.Append(TimeoutPrefix);
                writer.AppendInvariant(state.entry.TimeoutMilliseconds);
                writer.AppendOptional(EditorModePrefix, state.entry.EditorMode);
                writer.AppendOptional(OwnerKindPrefix, state.entry.OwnerKind);
                writer.AppendOptionalBool(CanShutdownProcessPrefix, state.entry.CanShutdownProcess);
                writer.AppendOptionalInvariant(ProcessIdPrefix, state.entry.ProcessId);
                writer.AppendOptional(LaunchAttemptIdPrefix, state.entry.LaunchAttemptId);
                writer.AppendOptional(StartupStatusPrefix, state.entry.StartupStatus);
                writer.AppendOptional(StartupBlockingReasonPrefix, state.entry.StartupBlockingReason);
                writer.AppendOptional(StartupPhasePrefix, state.entry.StartupPhase);
                writer.AppendOptional(RetryDispositionPrefix, state.entry.RetryDisposition);
                writer.AppendOptional(ErrorCodePrefix, state.entry.ErrorCode);
                writer.Append(' ');
                writer.Append(state.status);
            });
    }

    private static string CreateLifecycleSnapshotTextEntry (
        string eventName,
        DaemonStartLifecycleSnapshotProgressEntry entry)
    {
        var step = ContractLiteralCodec.TryParse<DaemonStartProgressEvent>(eventName, out var progressEvent)
            && progressEvent == DaemonStartProgressEvent.LifecycleObserved
            ? "lifecycle"
            : eventName;
        const string status = "observed";
        var projectFingerprint = entry.ProjectFingerprint.ToString();
        var length = checked(
            Prefix.Length
            + step.Length
            + ProjectPrefix.Length
            + projectFingerprint.Length
            + TimeoutPrefix.Length
            + SpanTextLength.GetInvariantInt64Length(entry.TimeoutMilliseconds)
            + SpanTextLength.GetOptionalStringLength(EditorModePrefix, entry.EditorMode)
            + LifecycleStatePrefix.Length
            + entry.LifecycleState.Length
            + SpanTextLength.GetOptionalStringLength(BlockingReasonPrefix, entry.BlockingReason)
            + CanAcceptExecutionRequestsPrefix.Length
            + SpanTextLength.GetBoolLength(entry.CanAcceptExecutionRequests)
            + 1
            + status.Length);
        return string.Create(
            length,
            (entry, step, projectFingerprint),
            static (destination, state) =>
            {
                var writer = new SpanTextWriter(destination);
                writer.Append(Prefix);
                writer.Append(state.step);
                writer.Append(ProjectPrefix);
                writer.Append(state.projectFingerprint);
                writer.Append(TimeoutPrefix);
                writer.AppendInvariant(state.entry.TimeoutMilliseconds);
                writer.AppendOptional(EditorModePrefix, state.entry.EditorMode);
                writer.Append(LifecycleStatePrefix);
                writer.Append(state.entry.LifecycleState);
                writer.AppendOptional(BlockingReasonPrefix, state.entry.BlockingReason);
                writer.Append(CanAcceptExecutionRequestsPrefix);
                writer.AppendBool(state.entry.CanAcceptExecutionRequests);
                writer.Append(' ');
                writer.Append(status);
            });
    }

    private static bool IsStartedEvent (DaemonStartProgressEvent? progressEvent)
    {
        return progressEvent
            is DaemonStartProgressEvent.Started
            or DaemonStartProgressEvent.PluginVerificationStarted
            or DaemonStartProgressEvent.SupervisorBootstrapStarted
            or DaemonStartProgressEvent.EnsureRunningStarted;
    }
}
