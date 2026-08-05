using System.Text.Json;
using MackySoft.Ucli.Application.Features.Recording.Artifacts;
using MackySoft.Ucli.Application.Features.Recording.Finalization;
using MackySoft.Ucli.Application.Features.Recording.Projection;
using MackySoft.Ucli.Application.Features.Recording.Registry;
using MackySoft.Ucli.Application.Features.Recording.Requests;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Recording;

namespace MackySoft.Ucli.Features.Recording.Finalization;

/// <summary>Publishes the independently verifiable terminal artifact set for one runtime recording.</summary>
internal sealed class GameViewRecordingTerminalFinalizer : IGameViewRecordingTerminalFinalizer
{
    private static readonly ArtifactKind[] ArtifactOrder =
    [
        GameViewRecordingArtifactKinds.Request,
        GameViewRecordingArtifactKinds.Video,
        GameViewRecordingArtifactKinds.PartialOutput,
        GameViewRecordingArtifactKinds.Cleanup,
        GameViewRecordingArtifactKinds.Manifest,
        GameViewRecordingArtifactKinds.TerminalRecord,
    ];

    private readonly IGameViewRecordingExecutionStore executionStore;

    public GameViewRecordingTerminalFinalizer (
        IGameViewRecordingExecutionStore executionStore)
    {
        this.executionStore = executionStore
            ?? throw new ArgumentNullException(nameof(executionStore));
    }

    public async ValueTask<GameViewRecordingTerminalFinalizationResult> FinalizeAsync (
        ProjectContext context,
        IGameViewRecordingArtifactLease artifactLease,
        GameViewRecordingStoredExecution stored,
        IpcGameViewRecordingTerminalSnapshot terminalSnapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(artifactLease);
        ArgumentNullException.ThrowIfNull(stored);
        ArgumentNullException.ThrowIfNull(terminalSnapshot);

        var validationError = ValidateInputs(stored, terminalSnapshot);
        if (validationError is not null)
        {
            return Failure(stored, validationError);
        }

        ArtifactSet artifacts;
        try
        {
            artifacts = ArtifactSet.Create(stored);
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidDataException)
        {
            return Failure(
                stored,
                FinalizationError(
                    $"The durable recording artifact checkpoint is invalid. {exception.Message}"));
        }

        var checkpoint = stored;
        var effectiveRequest = CreateEffectiveRequest(stored);
        var diagnostics = stored.Payload.Diagnostics.ToList();
        var videoDisposition = GameViewRecordingVideoDisposition.Missing;
        GameViewRecordingTimingObservation? timing = GetTiming(terminalSnapshot);
        var finalizationFailed = false;

        if (terminalSnapshot is IpcGameViewRecordingCompletedSnapshot completedSnapshot)
        {
            if (artifacts.Contains(GameViewRecordingArtifactKinds.PartialOutput)
                && !artifacts.Contains(GameViewRecordingArtifactKinds.Video))
            {
                finalizationFailed = true;
                AddOrUpdateDiagnostic(
                    diagnostics,
                    GameViewRecordingErrorCodes.FinalizationFailed,
                    "The finalized runtime output could not be published as a valid MP4.",
                    artifacts.Get(GameViewRecordingArtifactKinds.PartialOutput));
            }
            else
            {
                var videoResult = await artifactLease.PublishVideoAsync(
                        effectiveRequest,
                        terminalSnapshot.EncodedFrameCount,
                        artifacts.Get(GameViewRecordingArtifactKinds.Video),
                        cancellationToken)
                    .ConfigureAwait(false);
                if (videoResult.IsSuccess)
                {
                    var publication = videoResult.Publication!;
                    artifacts.Add(publication.Artifact);
                    videoDisposition = GameViewRecordingVideoDisposition.Available;
                    timing = MergeVideoTiming(completedSnapshot.Timing, publication);
                    checkpoint = await PersistCheckpointAsync(
                            context,
                            artifactLease,
                            checkpoint,
                            artifacts.Snapshot(),
                            diagnostics,
                            checked((int)publication.EncodedFrameCount),
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    finalizationFailed = true;
                    var partialResult = await RecoverPartialOutputAsync(
                            context,
                            artifactLease,
                            checkpoint,
                            artifacts,
                            diagnostics,
                            cancellationToken)
                        .ConfigureAwait(false);
                    checkpoint = partialResult.Checkpoint;
                    if (partialResult.Error is not null)
                    {
                        return Failure(checkpoint, partialResult.Error);
                    }

                    var partialRef = artifacts.Get(GameViewRecordingArtifactKinds.PartialOutput);
                    AddOrUpdateDiagnostic(
                        diagnostics,
                        GameViewRecordingErrorCodes.FinalizationFailed,
                        videoResult.Error!.Message,
                        partialRef);
                }
            }
        }
        else
        {
            if (artifacts.Contains(GameViewRecordingArtifactKinds.Video))
            {
                return Failure(
                    checkpoint,
                    FinalizationError(
                        "A non-completed runtime recording cannot own a finalized video checkpoint."));
            }

            var partialResult = await RecoverPartialOutputAsync(
                    context,
                    artifactLease,
                    checkpoint,
                    artifacts,
                    diagnostics,
                    cancellationToken)
                .ConfigureAwait(false);
            checkpoint = partialResult.Checkpoint;
            if (partialResult.Error is not null)
            {
                return Failure(checkpoint, partialResult.Error);
            }

            AttachPartialOutputToRuntimeFailure(diagnostics, terminalSnapshot, artifacts);
        }

        var stagingCleanup = artifactLease.CleanupProviderOutput();
        var cleanup = MergeCleanup(terminalSnapshot, stagingCleanup);
        if (cleanup.Disposition == GameViewRecordingCleanupDisposition.Failed)
        {
            AddOrUpdateDiagnostic(
                diagnostics,
                GameViewRecordingErrorCodes.CleanupFailed,
                stagingCleanup.Error?.Message
                    ?? "The recording runtime reported that owned state or resources were not fully restored.",
                artifact: null);
        }

        var state = ResolveTerminalState(
            terminalSnapshot,
            cleanup,
            videoDisposition,
            finalizationFailed);
        var summary = new GameViewRecordingTerminalSummary(
            state,
            terminalSnapshot.StopReason,
            videoDisposition,
            cleanup.Disposition,
            GetStartedAtUtc(terminalSnapshot),
            terminalSnapshot.CompletedAtUtc);

        var cleanupResult = await artifactLease.PublishCleanupAsync(
                cleanup,
                artifacts.Get(GameViewRecordingArtifactKinds.Cleanup),
                cancellationToken)
            .ConfigureAwait(false);
        if (!cleanupResult.IsSuccess)
        {
            return Failure(checkpoint, cleanupResult.Error!);
        }

        artifacts.Add(cleanupResult.Artifact!);
        checkpoint = await PersistCheckpointAsync(
                context,
                artifactLease,
                checkpoint,
                artifacts.Snapshot(),
                diagnostics,
                encodedFrameCount: null,
                cancellationToken)
            .ConfigureAwait(false);

        GameViewRecordingProviderIdentity provider;
        try
        {
            provider = CreateProviderIdentity(stored.StartCapability);
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException)
        {
            return Failure(
                checkpoint,
                FinalizationError(
                    $"The durable recording provider identity is incomplete. {exception.Message}"));
        }

        var manifestRefs = artifacts.SnapshotBefore(GameViewRecordingArtifactKinds.Manifest);
        GameViewRecordingManifest manifest;
        try
        {
            manifest = new GameViewRecordingManifest(
                GameViewRecordingManifest.CurrentSchemaVersion,
                stored.RecordingId,
                stored.RequestDigest,
                stored.Request,
                stored.Payload.Project,
                terminalSnapshot.Runtime,
                terminalSnapshot.StartGeneration,
                terminalSnapshot.ObservedGeneration,
                provider,
                GetTarget(terminalSnapshot),
                timing,
                summary,
                manifestRefs,
                diagnostics);
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException)
        {
            return Failure(
                checkpoint,
                FinalizationError(
                    $"The recording manifest facts are inconsistent. {exception.Message}"));
        }

        var manifestResult = await artifactLease.PublishManifestAsync(
                manifest,
                artifacts.Get(GameViewRecordingArtifactKinds.Manifest),
                cancellationToken)
            .ConfigureAwait(false);
        if (!manifestResult.IsSuccess)
        {
            return Failure(checkpoint, manifestResult.Error!);
        }

        artifacts.Add(manifestResult.Artifact!);
        checkpoint = await PersistCheckpointAsync(
                context,
                artifactLease,
                checkpoint,
                artifacts.Snapshot(),
                diagnostics,
                encodedFrameCount: null,
                cancellationToken)
            .ConfigureAwait(false);

        var terminalRecordRefs = artifacts.SnapshotBefore(GameViewRecordingArtifactKinds.TerminalRecord);
        var terminalRecord = new GameViewRecordingTerminalRecord(
            GameViewRecordingTerminalRecord.CurrentSchemaVersion,
            GameViewRecordingExecutionContract.Kind,
            stored.RecordingId,
            stored.RequestDigest,
            stored.Payload.Project,
            terminalSnapshot.Runtime,
            terminalSnapshot.StartGeneration,
            terminalSnapshot.ObservedGeneration,
            summary,
            stored.RequestRef,
            terminalRecordRefs,
            diagnostics);
        var terminalResult = await artifactLease.PublishTerminalRecordAsync(
                terminalRecord,
                artifacts.Get(GameViewRecordingArtifactKinds.TerminalRecord),
                cancellationToken)
            .ConfigureAwait(false);
        if (!terminalResult.IsSuccess)
        {
            return Failure(checkpoint, terminalResult.Error!);
        }

        artifacts.Add(terminalResult.Artifact!);
        checkpoint = await PersistCheckpointAsync(
                context,
                artifactLease,
                checkpoint,
                artifacts.Snapshot(),
                diagnostics,
                encodedFrameCount: null,
                cancellationToken)
            .ConfigureAwait(false);

        return GameViewRecordingTerminalFinalizationResult.Success(
            GameViewRecordingPayloadFactory.CreateTerminal(
                checkpoint,
                summary,
                terminalResult.Artifact!,
                artifacts.Snapshot(),
                diagnostics));
    }

    private async ValueTask<PartialRecoveryAttempt> RecoverPartialOutputAsync (
        ProjectContext context,
        IGameViewRecordingArtifactLease artifactLease,
        GameViewRecordingStoredExecution checkpoint,
        ArtifactSet artifacts,
        IReadOnlyList<GameViewRecordingDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var result = await artifactLease.RecoverPartialOutputAsync(
                artifacts.Get(GameViewRecordingArtifactKinds.PartialOutput),
                cancellationToken)
            .ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return new PartialRecoveryAttempt(checkpoint, result.Error);
        }
        if (result.Artifact is null)
        {
            return new PartialRecoveryAttempt(checkpoint, Error: null);
        }

        artifacts.Add(result.Artifact);
        var updated = await PersistCheckpointAsync(
                context,
                artifactLease,
                checkpoint,
                artifacts.Snapshot(),
                diagnostics,
                encodedFrameCount: null,
                cancellationToken)
            .ConfigureAwait(false);
        return new PartialRecoveryAttempt(updated, Error: null);
    }

    private async ValueTask<GameViewRecordingStoredExecution> PersistCheckpointAsync (
        ProjectContext context,
        IGameViewRecordingArtifactLease artifactLease,
        GameViewRecordingStoredExecution stored,
        IReadOnlyList<ArtifactRef> artifactRefs,
        IReadOnlyList<GameViewRecordingDiagnostic> diagnostics,
        int? encodedFrameCount,
        CancellationToken cancellationToken)
    {
        if (stored.Payload.Lifecycle != ExecutionLifecycle.Recovery)
        {
            throw new InvalidOperationException(
                "Terminal publication checkpoints require a recovery recording payload.");
        }

        var payload = GameViewRecordingPayloadFactory.CreateRecoveryCheckpoint(
            stored.Payload,
            encodedFrameCount,
            artifactRefs,
            diagnostics);
        var checkpoint = CopyStored(stored, payload);
        await executionStore.WriteAsync(
                context.UnityProject,
                artifactLease.ExecutionStatePath,
                checkpoint,
                cancellationToken)
            .ConfigureAwait(false);
        return checkpoint;
    }

    private static GameViewRecordingStoredExecution CopyStored (
        GameViewRecordingStoredExecution source,
        GameViewRecordingExecutionPayload payload) =>
        new(
            source.SchemaVersion,
            source.RecordingId,
            source.Request,
            source.CanonicalRequestJson,
            source.RequestDigest,
            source.RequestRef,
            source.StartCapability,
            source.StartBinding,
            source.StartDispatchDeadlineUtc,
            source.RuntimeSnapshot,
            payload);

    private static ExecutionError? ValidateInputs (
        GameViewRecordingStoredExecution stored,
        IpcGameViewRecordingTerminalSnapshot snapshot)
    {
        if (stored.Payload.Lifecycle != ExecutionLifecycle.Recovery)
        {
            return FinalizationError(
                "Terminal publication requires a durable recovery payload checkpoint.");
        }
        if (snapshot.RecordingId != stored.RecordingId
            || snapshot.RequestDigest != stored.RequestDigest
            || !RuntimeSnapshotsMatch(stored.RuntimeSnapshot, snapshot))
        {
            return FinalizationError(
                "The terminal runtime snapshot does not match the durable recording execution.");
        }
        return null;
    }

    private static bool RuntimeSnapshotsMatch (
        IpcGameViewRecordingSnapshot? expected,
        IpcGameViewRecordingSnapshot actual)
    {
        if (expected is null)
        {
            return false;
        }

        var expectedJson = JsonSerializer.SerializeToUtf8Bytes(
            expected,
            IpcJsonSerializerOptions.StrictPropertyNames);
        var actualJson = JsonSerializer.SerializeToUtf8Bytes(
            actual,
            IpcJsonSerializerOptions.StrictPropertyNames);
        return expectedJson.AsSpan().SequenceEqual(actualJson);
    }

    private static GameViewRecordingEffectiveRequest CreateEffectiveRequest (
        GameViewRecordingStoredExecution stored) =>
        new(
            stored.Request.SchemaVersion,
            stored.Request.Resolution,
            stored.Request.FrameRate,
            stored.Request.MaxDurationSeconds,
            stored.CanonicalRequestJson,
            stored.RequestDigest);

    private static GameViewRecordingProviderIdentity CreateProviderIdentity (
        GameViewRecordingCapability capability)
    {
        if (capability.Package.Version is null
            || capability.CaptureProfile is null
            || capability.Adapter.State != GameViewRecordingAdapterState.Registered)
        {
            throw new InvalidOperationException(
                "The start capability does not identify a registered Recorder adapter and capture profile.");
        }

        return new GameViewRecordingProviderIdentity(
            GameViewRecorderCompatibilityMetadata.PackageId,
            capability.Package.Version,
            GameViewRecorderCompatibilityMetadata.AdapterId,
            GameViewRecorderCompatibilityMetadata.AdapterVersion,
            capability.CaptureProfile);
    }

    private static GameViewRecordingTimingObservation MergeVideoTiming (
        GameViewRecordingTimingObservation timing,
        GameViewRecordingVideoPublication video)
    {
        return new GameViewRecordingTimingObservation(
            timing.MonotonicStartedTimestamp,
            timing.MonotonicStopRequestedTimestamp,
            timing.MonotonicCompletedTimestamp,
            timing.MonotonicFrequency,
            timing.GameTimeStartedSeconds,
            timing.GameTimeCompletedSeconds,
            timing.TimeScaleStarted,
            timing.TimeScaleCompleted,
            timing.FrameCountStarted,
            timing.FrameCountCompleted,
            video.DurationSeconds,
            checked((int)video.EncodedFrameCount),
            video.EffectiveFrameRate,
            timing.DroppedFrameCount,
            timing.DuplicatedFrameCount,
            timing.DelayedFrameCount);
    }

    private static GameViewRecordingCleanupRecord? GetCleanup (
        IpcGameViewRecordingTerminalSnapshot snapshot) =>
        snapshot switch
        {
            IpcGameViewRecordingCompletedSnapshot completed => completed.Cleanup,
            IpcGameViewRecordingFailedSnapshot failed => failed.Cleanup,
            IpcGameViewRecordingIndeterminateSnapshot indeterminate => indeterminate.Cleanup,
            _ => throw new ArgumentOutOfRangeException(nameof(snapshot)),
        };

    private static GameViewRecordingTargetObservation? GetTarget (
        IpcGameViewRecordingTerminalSnapshot snapshot) =>
        snapshot switch
        {
            IpcGameViewRecordingCompletedSnapshot completed => completed.Target,
            IpcGameViewRecordingFailedSnapshot failed => failed.Target,
            IpcGameViewRecordingIndeterminateSnapshot indeterminate => indeterminate.Target,
            _ => throw new ArgumentOutOfRangeException(nameof(snapshot)),
        };

    private static GameViewRecordingTimingObservation? GetTiming (
        IpcGameViewRecordingTerminalSnapshot snapshot) =>
        snapshot switch
        {
            IpcGameViewRecordingCompletedSnapshot completed => completed.Timing,
            IpcGameViewRecordingFailedSnapshot failed => failed.Timing,
            IpcGameViewRecordingIndeterminateSnapshot indeterminate => indeterminate.Timing,
            _ => throw new ArgumentOutOfRangeException(nameof(snapshot)),
        };

    private static DateTimeOffset? GetStartedAtUtc (
        IpcGameViewRecordingTerminalSnapshot snapshot) =>
        snapshot switch
        {
            IpcGameViewRecordingCompletedSnapshot completed => completed.StartedAtUtc,
            IpcGameViewRecordingFailedSnapshot failed => failed.StartedAtUtc,
            IpcGameViewRecordingIndeterminateSnapshot indeterminate => indeterminate.StartedAtUtc,
            _ => throw new ArgumentOutOfRangeException(nameof(snapshot)),
        };

    private static GameViewRecordingCleanupRecord MergeCleanup (
        IpcGameViewRecordingTerminalSnapshot snapshot,
        GameViewRecordingStagingCleanupResult stagingCleanup)
    {
        var runtimeCleanup = GetCleanup(snapshot);
        var restorations = runtimeCleanup?.StateRestorations
            ?? Enum.GetValues<GameViewRecordingStateRestorationKind>()
                .Select(static kind => new GameViewRecordingStateRestoration(
                    kind,
                    beforeValue: null,
                    afterValue: null,
                    changed: false,
                    restoreAttempted: false,
                    GameViewRecordingStateRestorationDisposition.Unconfirmed,
                    reasonCode: null))
                .ToArray();
        var runtimeReleases = runtimeCleanup?.ResourceReleases
            ?? Enum.GetValues<GameViewRecordingResourceKind>()
                .Select(static kind => new GameViewRecordingResourceRelease(
                    kind,
                    acquired: false,
                    releaseAttempted: false,
                    GameViewRecordingResourceReleaseDisposition.Unconfirmed,
                    reasonCode: null))
                .ToArray();
        var releases = runtimeReleases
            .Select(item => item.Kind == GameViewRecordingResourceKind.TemporaryOutput
                ? CreateTemporaryOutputRelease(stagingCleanup)
                : item)
            .ToArray();
        var disposition = ResolveCleanupDisposition(restorations, releases);
        return new GameViewRecordingCleanupRecord(
            GameViewRecordingCleanupRecord.CurrentSchemaVersion,
            snapshot.RecordingId,
            snapshot.RequestDigest,
            restorations,
            releases,
            disposition,
            snapshot.CompletedAtUtc);
    }

    private static GameViewRecordingResourceRelease CreateTemporaryOutputRelease (
        GameViewRecordingStagingCleanupResult stagingCleanup)
    {
        if (!stagingCleanup.IsSuccess)
        {
            return new GameViewRecordingResourceRelease(
                GameViewRecordingResourceKind.TemporaryOutput,
                acquired: true,
                releaseAttempted: true,
                GameViewRecordingResourceReleaseDisposition.Failed,
                GameViewRecordingErrorCodes.CleanupFailed);
        }

        return new GameViewRecordingResourceRelease(
            GameViewRecordingResourceKind.TemporaryOutput,
            acquired: true,
            releaseAttempted: true,
            GameViewRecordingResourceReleaseDisposition.Released,
            reasonCode: null);
    }

    private static GameViewRecordingCleanupDisposition ResolveCleanupDisposition (
        IReadOnlyList<GameViewRecordingStateRestoration> restorations,
        IReadOnlyList<GameViewRecordingResourceRelease> releases)
    {
        if (restorations.Any(static item =>
                item.Disposition == GameViewRecordingStateRestorationDisposition.Unconfirmed)
            || releases.Any(static item =>
                item.Disposition == GameViewRecordingResourceReleaseDisposition.Unconfirmed))
        {
            return GameViewRecordingCleanupDisposition.Unconfirmed;
        }
        if (restorations.Any(static item =>
                item.Disposition == GameViewRecordingStateRestorationDisposition.Failed)
            || releases.Any(static item =>
                item.Disposition == GameViewRecordingResourceReleaseDisposition.Failed))
        {
            return GameViewRecordingCleanupDisposition.Failed;
        }

        return GameViewRecordingCleanupDisposition.Complete;
    }

    private static GameViewRecordingState ResolveTerminalState (
        IpcGameViewRecordingTerminalSnapshot snapshot,
        GameViewRecordingCleanupRecord cleanup,
        GameViewRecordingVideoDisposition videoDisposition,
        bool finalizationFailed)
    {
        var hasDeterminateRuntimeOutcome = snapshot is IpcGameViewRecordingCompletedSnapshot
            or IpcGameViewRecordingFailedSnapshot;
        if (!hasDeterminateRuntimeOutcome
            || GetTarget(snapshot) is null
            || GetTiming(snapshot) is null
            || cleanup.Disposition == GameViewRecordingCleanupDisposition.Unconfirmed
            || videoDisposition == GameViewRecordingVideoDisposition.Unconfirmed)
        {
            return GameViewRecordingState.Indeterminate;
        }
        if (snapshot is IpcGameViewRecordingFailedSnapshot
            || cleanup.Disposition == GameViewRecordingCleanupDisposition.Failed
            || finalizationFailed
            || videoDisposition != GameViewRecordingVideoDisposition.Available)
        {
            return GameViewRecordingState.Failed;
        }

        return GameViewRecordingState.Completed;
    }

    private static void AttachPartialOutputToRuntimeFailure (
        List<GameViewRecordingDiagnostic> diagnostics,
        IpcGameViewRecordingTerminalSnapshot snapshot,
        ArtifactSet artifacts)
    {
        var partialRef = artifacts.Get(GameViewRecordingArtifactKinds.PartialOutput);
        if (snapshot is IpcGameViewRecordingFailedSnapshot failedSnapshot)
        {
            AddOrUpdateDiagnostic(
                diagnostics,
                failedSnapshot.Failure.Code,
                failedSnapshot.Failure.Message,
                partialRef);
            return;
        }

        if (snapshot is IpcGameViewRecordingIndeterminateSnapshot
            { Failure: { } indeterminateFailure })
        {
            AddOrUpdateDiagnostic(
                diagnostics,
                indeterminateFailure.Code,
                indeterminateFailure.Message,
                partialRef);
            return;
        }

        if (snapshot.StopReason is GameViewRecordingStopReason.PlayModeExited
            or GameViewRecordingStopReason.DomainReload
            or GameViewRecordingStopReason.UnityExited
            or GameViewRecordingStopReason.AdapterUnloaded)
        {
            AddOrUpdateDiagnostic(
                diagnostics,
                GameViewRecordingErrorCodes.Interrupted,
                "The Unity runtime interrupted the requested recording interval.",
                partialRef);
        }
    }

    private static void AddOrUpdateDiagnostic (
        List<GameViewRecordingDiagnostic> diagnostics,
        UcliCode code,
        string message,
        ArtifactRef? artifact)
    {
        var index = diagnostics.FindIndex(item => item.Code == code);
        if (index < 0)
        {
            diagnostics.Add(new GameViewRecordingDiagnostic(
                code,
                GameViewRecordingDiagnosticSeverity.Error,
                message,
                artifact is null ? [] : [artifact]));
            return;
        }
        if (artifact is null || diagnostics[index].ArtifactRefs.Contains(artifact))
        {
            return;
        }

        var current = diagnostics[index];
        diagnostics[index] = new GameViewRecordingDiagnostic(
            current.Code,
            current.Severity,
            current.Message,
            [.. current.ArtifactRefs, artifact]);
    }

    private static GameViewRecordingTerminalFinalizationResult Failure (
        GameViewRecordingStoredExecution stored,
        ExecutionError error)
    {
        if (stored.Payload.Lifecycle != ExecutionLifecycle.Recovery)
        {
            throw new InvalidOperationException(
                "A terminal finalization failure requires a recovery payload.");
        }

        return GameViewRecordingTerminalFinalizationResult.Failure(
            GameViewRecordingPayloadFactory.RequireRecovery(stored.Payload),
            error);
    }

    private static ExecutionError FinalizationError (string message) =>
        ExecutionError.InternalError(
            message,
            GameViewRecordingErrorCodes.FinalizationFailed);

    private sealed record PartialRecoveryAttempt (
        GameViewRecordingStoredExecution Checkpoint,
        ExecutionError? Error);

    private sealed class ArtifactSet
    {
        private readonly Dictionary<ArtifactKind, PathArtifactRef> artifacts;

        private ArtifactSet (Dictionary<ArtifactKind, PathArtifactRef> artifacts)
        {
            this.artifacts = artifacts;
        }

        public static ArtifactSet Create (GameViewRecordingStoredExecution stored)
        {
            var artifacts = new Dictionary<ArtifactKind, PathArtifactRef>();
            AddChecked(artifacts, stored.RequestRef);
            foreach (var artifact in stored.Payload.ArtifactRefs)
            {
                if (artifact is not PathArtifactRef pathArtifact)
                {
                    throw new InvalidDataException(
                        "Recording publication checkpoints must use repository-relative path artifacts.");
                }

                AddChecked(artifacts, pathArtifact);
            }

            return new ArtifactSet(artifacts);
        }

        public bool Contains (ArtifactKind kind) => artifacts.ContainsKey(kind);

        public PathArtifactRef? Get (ArtifactKind kind) =>
            artifacts.GetValueOrDefault(kind);

        public void Add (PathArtifactRef artifact)
        {
            AddChecked(artifacts, artifact);
        }

        public IReadOnlyList<ArtifactRef> Snapshot ()
        {
            var result = new List<ArtifactRef>(artifacts.Count);
            foreach (var kind in ArtifactOrder)
            {
                if (artifacts.TryGetValue(kind, out var artifact))
                {
                    result.Add(artifact);
                }
            }

            if (result.Count != artifacts.Count)
            {
                throw new InvalidDataException(
                    "Recording publication checkpoint contains an unknown artifact kind.");
            }

            return result.AsReadOnly();
        }

        public IReadOnlyList<ArtifactRef> SnapshotBefore (ArtifactKind exclusiveUpperBound)
        {
            var result = new List<ArtifactRef>(artifacts.Count);
            var foundUpperBound = false;
            foreach (var kind in ArtifactOrder)
            {
                if (kind == exclusiveUpperBound)
                {
                    foundUpperBound = true;
                    break;
                }
                if (artifacts.TryGetValue(kind, out var artifact))
                {
                    result.Add(artifact);
                }
            }

            if (!foundUpperBound || artifacts.Keys.Any(static kind => !ArtifactOrder.Contains(kind)))
            {
                throw new InvalidDataException(
                    "Recording publication checkpoint contains an unknown artifact kind.");
            }

            return result.AsReadOnly();
        }

        private static void AddChecked (
            Dictionary<ArtifactKind, PathArtifactRef> artifacts,
            PathArtifactRef artifact)
        {
            if (artifacts.TryGetValue(artifact.Kind, out var existing))
            {
                if (existing != artifact)
                {
                    throw new InvalidDataException(
                        $"Recording artifact kind '{artifact.Kind}' has conflicting durable references.");
                }

                return;
            }

            artifacts.Add(artifact.Kind, artifact);
        }
    }
}
