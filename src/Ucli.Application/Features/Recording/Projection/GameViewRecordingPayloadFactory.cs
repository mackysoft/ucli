using MackySoft.Ucli.Application.Features.Recording.Registry;
using MackySoft.Ucli.Application.Features.Recording.Requests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Projects;
using MackySoft.Ucli.Contracts.Recording;

namespace MackySoft.Ucli.Application.Features.Recording.Projection;

/// <summary>Projects host and runtime recording facts into the public execution contract.</summary>
internal static class GameViewRecordingPayloadFactory
{
    public static GameViewRecordingStopResultPayload RequireStopResult (
        GameViewRecordingExecutionPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (payload.Lifecycle == ExecutionLifecycle.Active
            || payload is not GameViewRecordingStopResultPayload stopResult)
        {
            throw new ArgumentException(
                "A recording stop result must be in the recovery or terminal lifecycle.",
                nameof(payload));
        }

        return stopResult;
    }

    public static GameViewRecordingRecoveryPayload RequireRecovery (
        GameViewRecordingExecutionPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (payload.Lifecycle != ExecutionLifecycle.Recovery
            || payload is not GameViewRecordingRecoveryPayload recovery)
        {
            throw new ArgumentException(
                "A recovery recording payload must be in the recovery lifecycle.",
                nameof(payload));
        }

        return recovery;
    }

    public static GameViewRecordingRecoveryPayload CreateRecoveryCheckpoint (
        GameViewRecordingExecutionPayload payload,
        int? encodedFrameCount,
        IReadOnlyList<ArtifactRef> artifactRefs,
        IReadOnlyList<GameViewRecordingDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(artifactRefs);
        ArgumentNullException.ThrowIfNull(diagnostics);
        if (payload.Lifecycle != ExecutionLifecycle.Recovery
            || payload.ExecutionReference is not RecoveryExecutionRef executionRef)
        {
            throw new ArgumentException(
                "A recovery checkpoint requires a recovery recording execution.",
                nameof(payload));
        }

        var progress = payload.RecordingProgress;
        return new GameViewRecordingRecoveryPayload(
            payload.Project,
            executionRef,
            payload.RequestDigest,
            payload.RequestRef,
            new GameViewRecordingRecoveryProgress(
                progress.State,
                progress.EffectiveMaxDurationSeconds,
                encodedFrameCount ?? progress.EncodedFrameCount,
                progress.StartedAtUtc,
                progress.StopRequestedAtUtc,
                progress.UpdatedAtUtc),
            artifactRefs,
            diagnostics);
    }

    public static GameViewRecordingActivePayload CreatePreparing (
        ResolvedUnityProjectContext project,
        Guid recordingId,
        GameViewRecordingEffectiveRequest request,
        ArtifactRef requestRef,
        DateTimeOffset observedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(requestRef);

        var progress = new GameViewRecordingActiveProgress(
            GameViewRecordingState.Preparing,
            request.MaxDurationSeconds,
            encodedFrameCount: null,
            startedAtUtc: null,
            stopRequestedAtUtc: null,
            observedAtUtc);
        return new GameViewRecordingActivePayload(
            CreateProject(project),
            CreateActiveExecutionRef(recordingId, request.Digest, progress.State),
            request.Digest,
            requestRef,
            progress,
            artifactRefs: [],
            diagnostics: []);
    }

    public static GameViewRecordingExecutionPayload CreateObservedNonTerminal (
        GameViewRecordingStoredExecution stored,
        IpcGameViewRecordingSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(stored);
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.RecordingId != stored.RecordingId
            || snapshot.RequestDigest != stored.RequestDigest)
        {
            throw new ArgumentException("Runtime recording identity does not match the durable execution.", nameof(snapshot));
        }

        var state = snapshot.IsTerminal
            ? GameViewRecordingState.Finalizing
            : snapshot.State;
        var diagnostics = MergeFailureDiagnostic(stored.Payload.Diagnostics, snapshot.ObservedFailure);
        return GameViewRecordingExecutionContract.GetLifecycle(state) switch
        {
            ExecutionLifecycle.Active => new GameViewRecordingActivePayload(
                stored.Payload.Project,
                CreateActiveExecutionRef(
                    stored.RecordingId,
                    stored.RequestDigest,
                    state),
                stored.RequestDigest,
                stored.RequestRef,
                new GameViewRecordingActiveProgress(
                    state,
                    snapshot.EffectiveMaxDurationSeconds,
                    snapshot.EncodedFrameCount,
                    snapshot.ObservedStartedAtUtc,
                    snapshot.ObservedStopRequestedAtUtc,
                    snapshot.UpdatedAtUtc),
                stored.Payload.ArtifactRefs,
                diagnostics),
            ExecutionLifecycle.Recovery => new GameViewRecordingRecoveryPayload(
                stored.Payload.Project,
                CreateRecoveryExecutionRef(
                    stored.RecordingId,
                    stored.RequestDigest,
                    state),
                stored.RequestDigest,
                stored.RequestRef,
                new GameViewRecordingRecoveryProgress(
                    state,
                    snapshot.EffectiveMaxDurationSeconds,
                    snapshot.EncodedFrameCount,
                    snapshot.ObservedStartedAtUtc,
                    snapshot.ObservedStopRequestedAtUtc,
                    snapshot.UpdatedAtUtc),
                stored.Payload.ArtifactRefs,
                diagnostics),
            _ => throw new InvalidOperationException("Runtime terminal observations must be finalized before public terminal projection."),
        };
    }

    public static GameViewRecordingTerminalPayload CreateTerminal (
        GameViewRecordingStoredExecution stored,
        GameViewRecordingTerminalSummary summary,
        ArtifactRef terminalRecordRef,
        IReadOnlyList<ArtifactRef> artifactRefs,
        IReadOnlyList<GameViewRecordingDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(stored);
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(terminalRecordRef);
        ArgumentNullException.ThrowIfNull(artifactRefs);
        ArgumentNullException.ThrowIfNull(diagnostics);

        var progress = new GameViewRecordingTerminalProgress(
            summary.State,
            stored.Request.MaxDurationSeconds,
            stored.Payload.RecordingProgress.EncodedFrameCount,
            summary.StartedAtUtc,
            stored.Payload.RecordingProgress.StopRequestedAtUtc,
            summary.CompletedAtUtc);
        return new GameViewRecordingTerminalPayload(
            stored.Payload.Project,
            CreateTerminalExecutionRef(
                stored.RecordingId,
                stored.RequestDigest,
                summary.State,
                terminalRecordRef),
            stored.RequestDigest,
            stored.RequestRef,
            progress,
            artifactRefs,
            diagnostics,
            summary);
    }

    private static UnityProjectIdentity CreateProject (ResolvedUnityProjectContext project) =>
        new(
            project.UnityProjectRoot.Value,
            project.ProjectFingerprint,
            project.UnityVersion);

    private static ActiveExecutionRef CreateActiveExecutionRef (
        Guid recordingId,
        MackySoft.Ucli.Contracts.Cryptography.Sha256Digest requestDigest,
        GameViewRecordingState state)
    {
        var locator = new ExecutionStatusLocator($"recording:{recordingId:D}");
        return new ActiveExecutionRef(
            GameViewRecordingExecutionContract.Kind,
            recordingId,
            requestDigest,
            GameViewRecordingExecutionContract.ToExecutionState(state),
            locator);
    }

    private static RecoveryExecutionRef CreateRecoveryExecutionRef (
        Guid recordingId,
        MackySoft.Ucli.Contracts.Cryptography.Sha256Digest requestDigest,
        GameViewRecordingState state) =>
        new(
            GameViewRecordingExecutionContract.Kind,
            recordingId,
            requestDigest,
            GameViewRecordingExecutionContract.ToExecutionState(state),
            new ExecutionStatusLocator($"recording:{recordingId:D}"));

    private static TerminalExecutionRef CreateTerminalExecutionRef (
        Guid recordingId,
        MackySoft.Ucli.Contracts.Cryptography.Sha256Digest requestDigest,
        GameViewRecordingState state,
        ArtifactRef terminalRecordRef) =>
        new(
            GameViewRecordingExecutionContract.Kind,
            recordingId,
            requestDigest,
            GameViewRecordingExecutionContract.ToExecutionState(state),
            statusLocator: null,
            terminalRecordRef ?? throw new ArgumentNullException(nameof(terminalRecordRef)));

    private static IReadOnlyList<GameViewRecordingDiagnostic> MergeFailureDiagnostic (
        IReadOnlyList<GameViewRecordingDiagnostic> established,
        IpcError? failure)
    {
        if (failure is null
            || established.Any(diagnostic => diagnostic.Code == failure.Code))
        {
            return established;
        }

        var result = new GameViewRecordingDiagnostic[established.Count + 1];
        for (var index = 0; index < established.Count; index++)
        {
            result[index] = established[index];
        }
        result[^1] = new GameViewRecordingDiagnostic(
            failure.Code,
            GameViewRecordingDiagnosticSeverity.Error,
            failure.Message,
            artifactRefs: []);
        return Array.AsReadOnly(result);
    }
}
