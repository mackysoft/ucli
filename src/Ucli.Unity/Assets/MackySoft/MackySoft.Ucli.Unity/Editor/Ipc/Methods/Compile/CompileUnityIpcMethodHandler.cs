using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Unity.Project;
using MackySoft.Ucli.Unity.Runtime;
using UnityEditor;
using UnityEditor.Compilation;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Handles <c>compile</c> IPC method requests. </summary>
    internal sealed class CompileUnityIpcMethodHandler : IRecoverableUnityIpcMethodHandler
    {
        private const long MaxCompileRequestBytes = 1024 * 1024;
        private const int RequiredStableLifecycleObservations = 2;

        private readonly IUnityEditorReadinessGate readinessGate;

        private readonly IpcProjectIdentity projectIdentity;

        private readonly IServerVersionProvider serverVersionProvider;

        private readonly IDaemonLogger daemonLogger;

        private readonly IUnityMutationLaneControl mutationLaneControl;

        /// <summary> Initializes a new instance of the <see cref="CompileUnityIpcMethodHandler" /> class. </summary>
        public CompileUnityIpcMethodHandler (
            IUnityEditorReadinessGate readinessGate,
            IpcProjectIdentity projectIdentity,
            IServerVersionProvider serverVersionProvider,
            IDaemonLogger daemonLogger,
            IUnityMutationLaneControl mutationLaneControl)
        {
            this.readinessGate = readinessGate ?? throw new ArgumentNullException(nameof(readinessGate));
            this.projectIdentity = projectIdentity ?? throw new ArgumentNullException(nameof(projectIdentity));
            this.serverVersionProvider = serverVersionProvider ?? throw new ArgumentNullException(nameof(serverVersionProvider));
            this.daemonLogger = daemonLogger ?? throw new ArgumentNullException(nameof(daemonLogger));
            this.mutationLaneControl = mutationLaneControl ?? throw new ArgumentNullException(nameof(mutationLaneControl));
        }

        /// <inheritdoc />
        public UnityIpcMethod Method => UnityIpcMethod.Compile;

        /// <inheritdoc />
        public bool TryCreateRecoverableRequestPayloadHash (
            ValidatedUnityIpcRequest request,
            out Sha256Digest requestPayloadHash,
            out IpcResponse errorResponse)
        {
            if (!TryReadCompileRequest(
                    request,
                    logDecodeFailure: false,
                    out IpcCompileRequest? compileRequest,
                    out errorResponse))
            {
                requestPayloadHash = null;
                return false;
            }

            // NOTE: compile retry identity is the requested run. The caller may resend with a
            // refreshed execution budget while recovering the same run after domain reload.
            var stablePayload = IpcPayloadCodec.SerializeToElement(new IpcCompileRequest(compileRequest!.RunId));
            requestPayloadHash = Sha256Digest.Compute(Encoding.UTF8.GetBytes(stablePayload.GetRawText()));
            return true;
        }

        /// <inheritdoc />
        public async ValueTask<IpcResponse> HandleAsync (
            ValidatedUnityIpcRequest request,
            IpcRequestCancellation cancellation)
        {
            return await HandleCoreAsync(request, null, cancellation);
        }

        /// <inheritdoc />
        public async ValueTask<IpcResponse> HandleRecoverableAsync (
            ValidatedUnityIpcRequest request,
            RecoverableIpcOperationContext context,
            IpcRequestCancellation cancellation)
        {
            return await HandleCoreAsync(request, context, cancellation);
        }

        private async ValueTask<IpcResponse> HandleCoreAsync (
            ValidatedUnityIpcRequest request,
            RecoverableIpcOperationContext recoverableContext,
            IpcRequestCancellation cancellation)
        {
            cancellation.Token.ThrowIfCancellationRequested();
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!TryReadCompileRequest(
                    request,
                    logDecodeFailure: true,
                    out IpcCompileRequest? compileRequest,
                    out var errorResponse))
            {
                return errorResponse!;
            }

            CompileArtifactPaths paths = null;
            try
            {
                paths = CompileArtifactPaths.Resolve(
                    projectIdentity.ProjectFingerprint,
                    compileRequest.RunId);

                if (recoverableContext != null
                    && recoverableContext.HasOperationRecord)
                {
                    var recoveredResponse = await TryRecoverPendingRunAsync(
                        request,
                        compileRequest,
                        paths,
                        recoverableContext,
                        cancellation.Token);
                    if (recoveredResponse != null)
                    {
                        return recoveredResponse;
                    }
                }

                var recorder = new CompileRunRecorder(
                    compileRequest.RunId,
                    projectIdentity,
                    readinessGate,
                    serverVersionProvider,
                    paths,
                    recoverableContext,
                    mutationLaneControl);
                // NOTE: Compile handling observes Unity Editor APIs across awaits, so it must remain on
                // Unity's synchronization context instead of resuming on a thread-pool thread.
                var summary = await recorder.ExecuteAsync(cancellation.Token);
                var response = new IpcCompileResponse(summary);
                return UnityIpcResponseFactory.CreateSuccessResponse(request, response);
            }
            catch (OperationCanceledException) when (
                cancellation.Reason == IpcRequestCancellationReason.ExecutionDeadline)
            {
                TryWriteAbandonedPendingRun(
                    paths,
                    readinessGate,
                    projectIdentity,
                    serverVersionProvider,
                    daemonLogger);
                return UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    IpcTransportErrorCodes.IpcTimeout,
                    "Unity compile assurance reached its request deadline.",
                    null);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (ArgumentException exception)
            {
                return UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    UcliCoreErrorCodes.InvalidArgument,
                    exception.Message,
                    null);
            }
            catch (Exception exception)
            {
                return UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    UcliCoreErrorCodes.InternalError,
                    $"Unity compile assurance failed. {exception.Message}",
                    null);
            }
        }

        private bool TryReadCompileRequest (
            ValidatedUnityIpcRequest request,
            bool logDecodeFailure,
            out IpcCompileRequest? compileRequest,
            out IpcResponse errorResponse)
        {
            if (!UnityIpcRequestCodec.TryDecodeCompileRequest(
                    request,
                    out compileRequest,
                    out errorResponse))
            {
                if (logDecodeFailure)
                {
                    daemonLogger.Warning(
                        DaemonLogCategories.Ipc,
                        "Compile payload decode failed.");
                }

                return false;
            }

            errorResponse = null;
            return true;
        }

        private async Task<IpcResponse> TryRecoverPendingRunAsync (
            ValidatedUnityIpcRequest request,
            IpcCompileRequest compileRequest,
            CompileArtifactPaths paths,
            RecoverableIpcOperationContext recoverableContext,
            CancellationToken cancellationToken)
        {
            if (!recoverableContext.TryReadPendingPayload<IpcCompileSummary>(
                    out var pendingSummary,
                    out var errorMessage))
            {
                return UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    UcliCoreErrorCodes.InternalError,
                    $"Recoverable compile operation state could not be read. {errorMessage}",
                    null);
            }

            if (!IsRecoverablePendingSummary(
                    pendingSummary,
                    compileRequest,
                    projectIdentity,
                    out var invalidStateMessage))
            {
                return UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    UcliCoreErrorCodes.InternalError,
                    invalidStateMessage,
                    null);
            }

            if (TryReadPendingSummary(paths.SummaryJsonPath, daemonLogger, out var completedSummary)
                && completedSummary.Completed
                && MatchesPendingSummaryIdentity(completedSummary, pendingSummary))
            {
                // NOTE: summary.json is a public compile artifact, so recovery may reuse it
                // only when it belongs to the same pending run that the operation store restored.
                return CreateCompileSuccessResponse(request, completedSummary);
            }

            if (!File.Exists(paths.RequestJsonPath))
            {
                WriteJsonAtomically(paths.RequestJsonPath, pendingSummary);
            }

            var afterSnapshot = await WaitUntilCompileSettledAsync(readinessGate, cancellationToken);
            var finalSummary = CanCompletePendingRunFromRecovery(pendingSummary, afterSnapshot)
                ? CreateFinalSummary(
                    pendingSummary,
                    afterSnapshot,
                    projectIdentity.UnityVersion,
                    serverVersionProvider,
                    DateTimeOffset.UtcNow,
                    diagnostics: new DiagnosticAccumulator())
                : CreateAbandonedPendingSummary(
                    pendingSummary,
                    afterSnapshot,
                    projectIdentity.UnityVersion,
                    serverVersionProvider,
                    DateTimeOffset.UtcNow);
            WriteDiagnostics(paths.DiagnosticsJsonPath, finalSummary);
            WriteJsonAtomically(paths.SummaryJsonPath, finalSummary);
            return CreateCompileSuccessResponse(request, finalSummary);
        }

        private static IpcResponse CreateCompileSuccessResponse (
            ValidatedUnityIpcRequest request,
            IpcCompileSummary summary)
        {
            var response = new IpcCompileResponse(summary);
            return UnityIpcResponseFactory.CreateSuccessResponse(request, response);
        }

        private static bool IsRecoverablePendingSummary (
            IpcCompileSummary pendingSummary,
            IpcCompileRequest compileRequest,
            IpcProjectIdentity projectIdentity,
            out string errorMessage)
        {
            if (pendingSummary.Completed)
            {
                errorMessage = "Recoverable compile operation pending summary is already completed.";
                return false;
            }

            if (pendingSummary.RunId != compileRequest.RunId)
            {
                errorMessage = "Recoverable compile operation runId does not match the retry request.";
                return false;
            }

            if (pendingSummary.ProjectFingerprint != projectIdentity.ProjectFingerprint)
            {
                errorMessage = "Recoverable compile operation project fingerprint does not match this daemon.";
                return false;
            }

            if (!string.Equals(
                    pendingSummary.Lifecycle.UnityVersion,
                    projectIdentity.UnityVersion,
                    StringComparison.Ordinal))
            {
                errorMessage = "Recoverable compile operation Unity version does not match this daemon.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        private static bool MatchesPendingSummaryIdentity (
            IpcCompileSummary completedSummary,
            IpcCompileSummary pendingSummary)
        {
            return completedSummary.RunId == pendingSummary.RunId
                && completedSummary.ProjectFingerprint == pendingSummary.ProjectFingerprint
                && completedSummary.StartedAtUtc == pendingSummary.StartedAtUtc
                && string.Equals(
                    completedSummary.Lifecycle.UnityVersion,
                    pendingSummary.Lifecycle.UnityVersion,
                    StringComparison.Ordinal);
        }

        private static bool TryReadPendingSummary (
            string requestPath,
            IDaemonLogger daemonLogger,
            out IpcCompileSummary pendingSummary)
        {
            pendingSummary = null;
            if (!File.Exists(requestPath) && !Directory.Exists(requestPath))
            {
                return false;
            }

            try
            {
                pendingSummary = JsonSerializer.Deserialize<IpcCompileSummary>(
                    ReadAllTextBounded(
                        requestPath,
                        MaxCompileRequestBytes,
                        "Compile pending-run request"),
                    IpcJsonSerializerOptions.Default);
                return pendingSummary != null;
            }
            catch (Exception exception)
            {
                daemonLogger.Warning(
                    DaemonLogCategories.Ipc,
                    $"Compile pending-run request could not be read and was skipped: {requestPath}. {exception.Message}");
                return false;
            }
        }

        private static string ReadAllTextBounded (
            string path,
            long maxBytes,
            string artifactDescription)
        {
            EnsureReadableArtifactPath(path, maxBytes, artifactDescription);
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var memoryStream = new MemoryStream())
            {
                if (stream.Length > maxBytes)
                {
                    throw new IOException($"{artifactDescription} exceeded {maxBytes} bytes: {path}");
                }

                var buffer = new byte[8192];
                long totalBytesRead = 0;
                while (true)
                {
                    var bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 0)
                    {
                        break;
                    }

                    totalBytesRead += bytesRead;
                    if (totalBytesRead > maxBytes)
                    {
                        throw new IOException($"{artifactDescription} exceeded {maxBytes} bytes: {path}");
                    }

                    memoryStream.Write(buffer, 0, bytesRead);
                }

                return Encoding.UTF8.GetString(memoryStream.ToArray());
            }
        }

        private static void EnsureReadableArtifactPath (
            string path,
            long maxBytes,
            string artifactDescription)
        {
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                throw new FileNotFoundException($"{artifactDescription} was not found: {path}", path);
            }

            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException($"{artifactDescription} source must not be a reparse point: {path}");
            }

            if ((attributes & FileAttributes.Directory) != 0)
            {
                throw new IOException($"{artifactDescription} source must not be a directory: {path}");
            }

            var fileInfo = new FileInfo(path);
            if (fileInfo.Length > maxBytes)
            {
                throw new IOException($"{artifactDescription} exceeded {maxBytes} bytes: {path}");
            }
        }

        private static void EnsureWritableArtifactPath (string path)
        {
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                return;
            }

            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException($"Compile artifact target must not be a reparse point: {path}");
            }

            if ((attributes & FileAttributes.Directory) != 0)
            {
                throw new IOException($"Compile artifact target must not be a directory: {path}");
            }
        }

        private static void TryWriteAbandonedPendingRun (
            CompileArtifactPaths paths,
            IUnityEditorReadinessGate readinessGate,
            IpcProjectIdentity projectIdentity,
            IServerVersionProvider serverVersionProvider,
            IDaemonLogger daemonLogger)
        {
            if (paths == null)
            {
                return;
            }

            if (!TryReadPendingSummary(paths.RequestJsonPath, daemonLogger, out var pendingSummary)
                || pendingSummary.Completed)
            {
                return;
            }

            if (TryReadPendingSummary(paths.SummaryJsonPath, daemonLogger, out var existingSummary)
                && existingSummary.Completed
                && MatchesPendingSummaryIdentity(existingSummary, pendingSummary))
            {
                return;
            }

            try
            {
                var snapshot = readinessGate.CaptureObservation();
                var abandonedSummary = CreateAbandonedPendingSummary(
                    pendingSummary,
                    snapshot,
                    projectIdentity.UnityVersion,
                    serverVersionProvider,
                    DateTimeOffset.UtcNow);
                WriteDiagnostics(paths.DiagnosticsJsonPath, abandonedSummary);
                WriteJsonAtomically(paths.SummaryJsonPath, abandonedSummary);
            }
            catch (Exception exception)
            {
                daemonLogger.Warning(
                    DaemonLogCategories.Ipc,
                    $"Compile pending-run timeout summary could not be written. {exception.Message}");
            }
        }

        private static IpcCompileSummary CreatePendingSummary (
            Guid runId,
            IpcProjectIdentity projectIdentity,
            UnityEditorObservation beforeSnapshot,
            IServerVersionProvider serverVersionProvider,
            DateTimeOffset startedAtUtc)
        {
            return new IpcCompileSummary(
                RunId: runId,
                ProjectFingerprint: projectIdentity.ProjectFingerprint,
                Completed: false,
                StartedAtUtc: startedAtUtc,
                CompletedAtUtc: null,
                Refresh: new IpcCompileSummary.RefreshEvidence(
                    Origin: CompileRefreshOrigin.AssetDatabaseRefresh,
                    Requested: true,
                    StartedAtUtc: startedAtUtc,
                    CompletedAtUtc: null,
                    Completed: false),
                ScriptCompilation: new IpcCompileSummary.ScriptCompilationEvidence(
                    Started: false,
                    Completed: false,
                    CompileGenerationBefore: beforeSnapshot.State.Generations.CompileGeneration,
                    CompileGenerationAfter: beforeSnapshot.State.Generations.CompileGeneration,
                    Diagnostics: new IpcCompileSummary.DiagnosticsEvidence(0, 0, null)),
                DomainReload: new IpcCompileSummary.DomainReloadEvidence(
                    ReloadRequired: false,
                    ReloadObserved: false,
                    GenerationBefore: beforeSnapshot.State.Generations.DomainReloadGeneration,
                    GenerationAfter: beforeSnapshot.State.Generations.DomainReloadGeneration,
                    Settled: false),
                Lifecycle: CreateLifecycleEvidence(
                    beforeSnapshot,
                    projectIdentity.UnityVersion,
                    serverVersionProvider));
        }

        private static IpcCompileSummary CreateFinalSummary (
            IpcCompileSummary pendingSummary,
            UnityEditorObservation afterSnapshot,
            string unityVersion,
            IServerVersionProvider serverVersionProvider,
            DateTimeOffset completedAtUtc,
            DiagnosticAccumulator diagnostics)
        {
            var primaryDiagnostic = diagnostics.PrimaryDiagnostic ?? afterSnapshot.PrimaryDiagnostic;
            var errorCount = diagnostics.ErrorCount;
            if (errorCount == 0 && primaryDiagnostic != null)
            {
                errorCount = 1;
            }

            var domainReloadObserved = IsDomainReloadObserved(pendingSummary, afterSnapshot);
            return new IpcCompileSummary(
                RunId: pendingSummary.RunId,
                ProjectFingerprint: pendingSummary.ProjectFingerprint,
                Completed: true,
                StartedAtUtc: pendingSummary.StartedAtUtc,
                CompletedAtUtc: completedAtUtc,
                Refresh: new IpcCompileSummary.RefreshEvidence(
                    Origin: pendingSummary.Refresh.Origin,
                    Requested: pendingSummary.Refresh.Requested,
                    StartedAtUtc: pendingSummary.Refresh.StartedAtUtc,
                    CompletedAtUtc: completedAtUtc,
                    Completed: true),
                ScriptCompilation: new IpcCompileSummary.ScriptCompilationEvidence(
                    Started: diagnostics.CompilationStarted
                        || pendingSummary.ScriptCompilation.CompileGenerationBefore
                            != afterSnapshot.State.Generations.CompileGeneration,
                    Completed: true,
                    CompileGenerationBefore: pendingSummary.ScriptCompilation.CompileGenerationBefore,
                    CompileGenerationAfter: afterSnapshot.State.Generations.CompileGeneration,
                    Diagnostics: new IpcCompileSummary.DiagnosticsEvidence(
                        ErrorCount: errorCount,
                        WarningCount: diagnostics.WarningCount,
                        PrimaryDiagnostic: primaryDiagnostic)),
                DomainReload: new IpcCompileSummary.DomainReloadEvidence(
                    ReloadRequired: domainReloadObserved,
                    ReloadObserved: domainReloadObserved,
                    GenerationBefore: pendingSummary.DomainReload.GenerationBefore,
                    GenerationAfter: afterSnapshot.State.Generations.DomainReloadGeneration,
                    Settled: IsLifecycleSettled(afterSnapshot)),
                Lifecycle: CreateLifecycleEvidence(
                    afterSnapshot,
                    unityVersion,
                    serverVersionProvider));
        }

        private static IpcCompileSummary CreateAbandonedPendingSummary (
            IpcCompileSummary pendingSummary,
            UnityEditorObservation afterSnapshot,
            string unityVersion,
            IServerVersionProvider serverVersionProvider,
            DateTimeOffset completedAtUtc)
        {
            var primaryDiagnostic = afterSnapshot.PrimaryDiagnostic;
            var errorCount = primaryDiagnostic == null ? 0 : 1;
            var domainReloadObserved = IsDomainReloadObserved(pendingSummary, afterSnapshot);
            return new IpcCompileSummary(
                RunId: pendingSummary.RunId,
                ProjectFingerprint: pendingSummary.ProjectFingerprint,
                Completed: true,
                StartedAtUtc: pendingSummary.StartedAtUtc,
                CompletedAtUtc: completedAtUtc,
                Refresh: new IpcCompileSummary.RefreshEvidence(
                    Origin: pendingSummary.Refresh.Origin,
                    Requested: pendingSummary.Refresh.Requested,
                    StartedAtUtc: pendingSummary.Refresh.StartedAtUtc,
                    CompletedAtUtc: null,
                    Completed: false),
                ScriptCompilation: new IpcCompileSummary.ScriptCompilationEvidence(
                    Started: false,
                    Completed: false,
                    CompileGenerationBefore: pendingSummary.ScriptCompilation.CompileGenerationBefore,
                    CompileGenerationAfter: afterSnapshot.State.Generations.CompileGeneration,
                    Diagnostics: new IpcCompileSummary.DiagnosticsEvidence(
                        ErrorCount: errorCount,
                        WarningCount: 0,
                        PrimaryDiagnostic: primaryDiagnostic)),
                DomainReload: new IpcCompileSummary.DomainReloadEvidence(
                    ReloadRequired: domainReloadObserved,
                    ReloadObserved: domainReloadObserved,
                    GenerationBefore: pendingSummary.DomainReload.GenerationBefore,
                    GenerationAfter: afterSnapshot.State.Generations.DomainReloadGeneration,
                    Settled: IsLifecycleSettled(afterSnapshot)),
                Lifecycle: CreateLifecycleEvidence(
                    afterSnapshot,
                    unityVersion,
                    serverVersionProvider));
        }

        private static bool CanCompletePendingRunFromRecovery (
            IpcCompileSummary pendingSummary,
            UnityEditorObservation afterSnapshot)
        {
            // NOTE: A pending artifact is written before AssetDatabase.Refresh so it can survive
            // domain reload. Recovery may only claim a completed compile when persisted or
            // lifecycle evidence proves the refresh crossed a compile/reload boundary.
            return pendingSummary.Refresh.Completed
                || pendingSummary.ScriptCompilation.Started
                || IsDomainReloadObserved(pendingSummary, afterSnapshot);
        }

        private static bool IsDomainReloadObserved (
            IpcCompileSummary pendingSummary,
            UnityEditorObservation afterSnapshot)
        {
            return pendingSummary.DomainReload.GenerationBefore
                != afterSnapshot.State.Generations.DomainReloadGeneration;
        }

        private static IpcCompileSummary.LifecycleEvidence CreateLifecycleEvidence (
            UnityEditorObservation snapshot,
            string unityVersion,
            IServerVersionProvider serverVersionProvider)
        {
            return new IpcCompileSummary.LifecycleEvidence(
                ServerVersion: serverVersionProvider.GetVersion(),
                UnityVersion: unityVersion,
                State: snapshot.State,
                ObservedAtUtc: snapshot.ObservedAtUtc,
                ActionRequired: snapshot.ActionRequired,
                PrimaryDiagnostic: snapshot.PrimaryDiagnostic);
        }

        private static bool IsLifecycleSettled (UnityEditorObservation snapshot)
        {
            return snapshot.State.LifecycleState is not IpcEditorLifecycleState.DomainReloading
                and not IpcEditorLifecycleState.Compiling
                and not IpcEditorLifecycleState.Reimporting
                and not IpcEditorLifecycleState.Recovering
                and not IpcEditorLifecycleState.Starting;
        }

        private static async Task<UnityEditorObservation> WaitUntilCompileSettledAsync (
            IUnityEditorReadinessGate readinessGate,
            CancellationToken cancellationToken)
        {
            var observationWindow = new SettledLifecycleObservationWindow();
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var snapshot = readinessGate.CaptureObservation();
                if (observationWindow.Observe(snapshot))
                {
                    return snapshot;
                }

                await UnityEditorUpdateAwaiter.WaitForNextUpdateAsync(cancellationToken);
            }
        }

        private static void WriteDiagnostics (
            string path,
            IpcCompileSummary summary)
        {
            WriteJsonAtomically(path, new
            {
                summary.RunId,
                summary.ScriptCompilation.Diagnostics.ErrorCount,
                summary.ScriptCompilation.Diagnostics.WarningCount,
                summary.ScriptCompilation.Diagnostics.PrimaryDiagnostic,
            });
        }

        private static void WriteJsonAtomically<T> (
            string path,
            T value)
        {
            var directoryPath = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                throw new InvalidOperationException($"Artifact directory path could not be resolved: {path}");
            }

            FileSystemAccessBoundary.EnsureSecureDirectory(directoryPath);
            var tempPath = path + $".tmp.{Guid.NewGuid():N}";

            try
            {
                EnsureWritableArtifactPath(tempPath);
                using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
                {
                    writer.Write(JsonSerializer.Serialize(value, IpcJsonSerializerOptions.Default));
                }

                FileSystemAccessBoundary.EnsureSecureFile(tempPath);
                EnsureWritableArtifactPath(path);
                ReplaceFile(tempPath, path);
                FileSystemAccessBoundary.EnsureSecureFile(path);
            }
            finally
            {
                DeleteTemporaryFileIfExists(tempPath);
            }
        }

        private static void ReplaceFile (
            string temporaryPath,
            string path)
        {
            try
            {
                File.Replace(temporaryPath, path, destinationBackupFileName: null, ignoreMetadataErrors: true);
            }
            catch (FileNotFoundException)
            {
                MoveOrReplaceWhenCreatedConcurrently(temporaryPath, path);
            }
            catch (IOException) when (!File.Exists(path))
            {
                MoveOrReplaceWhenCreatedConcurrently(temporaryPath, path);
            }
        }

        private static void MoveOrReplaceWhenCreatedConcurrently (
            string temporaryPath,
            string path)
        {
            try
            {
                File.Move(temporaryPath, path);
            }
            catch (IOException) when (File.Exists(path))
            {
                EnsureWritableArtifactPath(path);
                File.Replace(temporaryPath, path, destinationBackupFileName: null, ignoreMetadataErrors: true);
            }
        }

        private static void DeleteTemporaryFileIfExists (string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }


        private sealed class SettledLifecycleObservationWindow
        {
            private int stableUpdates;

            private bool hasStableSnapshot;

            private IpcEditorLifecycleState stableLifecycleState;

            private IpcCompileState stableCompileState;

            private long stableCompileGeneration;

            private long stableDomainReloadGeneration;

            public bool Observe (UnityEditorObservation snapshot)
            {
                if (!IsLifecycleSettled(snapshot))
                {
                    Reset();
                    return false;
                }

                if (!hasStableSnapshot || !MatchesStableSnapshot(snapshot))
                {
                    stableUpdates = 0;
                    CaptureStableSnapshot(snapshot);
                }

                stableUpdates++;
                return stableUpdates >= RequiredStableLifecycleObservations;
            }

            private bool MatchesStableSnapshot (UnityEditorObservation snapshot)
            {
                return stableLifecycleState == snapshot.State.LifecycleState
                    && stableCompileState == snapshot.State.CompileState
                    && stableCompileGeneration == snapshot.State.Generations.CompileGeneration
                    && stableDomainReloadGeneration == snapshot.State.Generations.DomainReloadGeneration;
            }

            private void CaptureStableSnapshot (UnityEditorObservation snapshot)
            {
                hasStableSnapshot = true;
                stableLifecycleState = snapshot.State.LifecycleState;
                stableCompileState = snapshot.State.CompileState;
                stableCompileGeneration = snapshot.State.Generations.CompileGeneration;
                stableDomainReloadGeneration = snapshot.State.Generations.DomainReloadGeneration;
            }

            private void Reset ()
            {
                stableUpdates = 0;
                hasStableSnapshot = false;
                stableLifecycleState = default;
                stableCompileState = default;
                stableCompileGeneration = default;
                stableDomainReloadGeneration = default;
            }
        }

        private sealed class CompileRunRecorder : IDisposable
        {
            private readonly Guid runId;

            private readonly IpcProjectIdentity projectIdentity;

            private readonly IUnityEditorReadinessGate readinessGate;

            private readonly IServerVersionProvider serverVersionProvider;

            private readonly CompileArtifactPaths paths;

            private readonly RecoverableIpcOperationContext recoverableContext;

            private readonly IUnityMutationLaneControl mutationLaneControl;

            private readonly DiagnosticAccumulator diagnostics = new DiagnosticAccumulator();

            public CompileRunRecorder (
                Guid runId,
                IpcProjectIdentity projectIdentity,
                IUnityEditorReadinessGate readinessGate,
                IServerVersionProvider serverVersionProvider,
                CompileArtifactPaths paths,
                RecoverableIpcOperationContext recoverableContext,
                IUnityMutationLaneControl mutationLaneControl)
            {
                this.runId = runId;
                this.projectIdentity = projectIdentity;
                this.readinessGate = readinessGate;
                this.serverVersionProvider = serverVersionProvider;
                this.paths = paths;
                this.recoverableContext = recoverableContext;
                this.mutationLaneControl = mutationLaneControl;
            }

            public async Task<IpcCompileSummary> ExecuteAsync (CancellationToken cancellationToken)
            {
                var beforeSnapshot = readinessGate.CaptureObservation();
                var startedAtUtc = DateTimeOffset.UtcNow;
                var pendingSummary = CreatePendingSummary(
                    runId,
                    projectIdentity,
                    beforeSnapshot,
                    serverVersionProvider,
                    startedAtUtc);

                if (recoverableContext != null)
                {
                    var recoveryWriteResult = await recoverableContext.MarkPendingAsync(
                        pendingSummary,
                        cancellationToken);
                    if (!recoveryWriteResult.IsSuccess)
                    {
                        throw new InvalidOperationException(
                            $"Compile recovery state could not be persisted. {recoveryWriteResult.ErrorMessage}");
                    }
                }

                // NOTE: Remove completed outputs only after pending recovery state is durable.
                // A domain reload here can still resume from request.json and the operation store.
                DeleteCompletedRunArtifacts(paths);
                WriteJsonAtomically(paths.RequestJsonPath, pendingSummary);

                var mutationActivity = mutationLaneControl.BeginMutation();
                Task<UnityEditorObservation> settleTask = null;
                try
                {
                    Subscribe();
                    settleTask = WaitUntilCompileSettledAsync(readinessGate, CancellationToken.None);
                    _ = settleTask.ContinueWith(
                        static (completedTask, state) =>
                        {
                            _ = completedTask.Exception;
                            if (completedTask.Status == TaskStatus.RanToCompletion)
                            {
                                ((IUnityMutationActivity)state).Complete();
                            }
                        },
                        mutationActivity,
                        CancellationToken.None,
                        TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);
                    AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                    var afterSnapshot = await AwaitWithCancellationAsync(settleTask, cancellationToken);
                    var completedAtUtc = DateTimeOffset.UtcNow;
                    var finalSummary = CreateFinalSummary(
                        pendingSummary,
                        afterSnapshot,
                        projectIdentity.UnityVersion,
                        serverVersionProvider,
                        completedAtUtc,
                        diagnostics);
                    WriteDiagnostics(paths.DiagnosticsJsonPath, finalSummary);
                    WriteJsonAtomically(paths.SummaryJsonPath, finalSummary);
                    return finalSummary;
                }
                finally
                {
                    if (settleTask == null)
                    {
                        mutationActivity.Complete();
                    }

                    Dispose();
                }
            }

            private static async Task<T> AwaitWithCancellationAsync<T> (
                Task<T> task,
                CancellationToken cancellationToken)
            {
                if (!cancellationToken.CanBeCanceled || task.IsCompleted)
                {
                    return await task;
                }

                var cancellationSource = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                using (cancellationToken.Register(
                           static state => ((TaskCompletionSource<bool>)state).TrySetResult(true),
                           cancellationSource))
                {
                    if (!ReferenceEquals(
                            await Task.WhenAny(task, cancellationSource.Task),
                            task))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                }

                return await task;
            }

            public void Dispose ()
            {
                CompilationPipeline.compilationStarted -= OnCompilationStarted;
                CompilationPipeline.assemblyCompilationFinished -= OnAssemblyCompilationFinished;
                CompilationPipeline.compilationFinished -= OnCompilationFinished;
            }

            private void Subscribe ()
            {
                CompilationPipeline.compilationStarted -= OnCompilationStarted;
                CompilationPipeline.assemblyCompilationFinished -= OnAssemblyCompilationFinished;
                CompilationPipeline.compilationFinished -= OnCompilationFinished;
                CompilationPipeline.compilationStarted += OnCompilationStarted;
                CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;
                CompilationPipeline.compilationFinished += OnCompilationFinished;
            }

            private void OnCompilationStarted (object _)
            {
                diagnostics.CompilationStarted = true;
            }

            private void OnCompilationFinished (object _)
            {
                diagnostics.CompilationCompleted = true;
            }

            private void OnAssemblyCompilationFinished (
                string _,
                CompilerMessage[] messages)
            {
                diagnostics.Add(messages);
            }
        }

        private static void DeleteCompletedRunArtifacts (CompileArtifactPaths paths)
        {
            FileUtilities.DeleteIfExists(paths.SummaryJsonPath);
            FileUtilities.DeleteIfExists(paths.DiagnosticsJsonPath);
        }

        private sealed class DiagnosticAccumulator
        {
            public bool CompilationStarted { get; set; }

            public bool CompilationCompleted { get; set; }

            public int ErrorCount { get; private set; }

            public int WarningCount { get; private set; }

            public IpcPrimaryDiagnostic PrimaryDiagnostic { get; private set; }

            public void Add (CompilerMessage[] messages)
            {
                if (messages == null)
                {
                    return;
                }

                for (var i = 0; i < messages.Length; i++)
                {
                    var message = messages[i];
                    if (message.type == CompilerMessageType.Error)
                    {
                        ErrorCount++;
                        PrimaryDiagnostic = PrimaryDiagnostic ?? CreateDiagnostic(message);
                    }
                    else if (message.type == CompilerMessageType.Warning)
                    {
                        WarningCount++;
                    }
                }
            }

            private static IpcPrimaryDiagnostic CreateDiagnostic (CompilerMessage message)
            {
                return new IpcPrimaryDiagnostic(
                    Kind: DaemonDiagnosisPrimaryDiagnosticKind.Compiler,
                    Code: TryExtractCompilerCode(message.message),
                    File: string.IsNullOrWhiteSpace(message.file) ? null : message.file,
                    Line: message.line > 0 ? message.line : null,
                    Column: message.column > 0 ? message.column : null,
                    Message: string.IsNullOrWhiteSpace(message.message) ? null : message.message.Trim());
            }

            private static string TryExtractCompilerCode (string message)
            {
                if (string.IsNullOrWhiteSpace(message))
                {
                    return null;
                }

                for (var i = 0; i < message.Length - 2; i++)
                {
                    if (char.ToUpperInvariant(message[i]) != 'C'
                        || char.ToUpperInvariant(message[i + 1]) != 'S'
                        || !char.IsDigit(message[i + 2]))
                    {
                        continue;
                    }

                    var end = i + 3;
                    while (end < message.Length && char.IsDigit(message[end]))
                    {
                        end++;
                    }

                    return message.Substring(i, end - i).ToUpperInvariant();
                }

                return null;
            }
        }

        private sealed record CompileArtifactPaths (
            string ArtifactsDir,
            string RequestJsonPath,
            string SummaryJsonPath,
            string DiagnosticsJsonPath)
        {
            public static CompileArtifactPaths Resolve (
                ProjectFingerprint projectFingerprint,
                Guid runId)
            {
                var artifactsDir = UcliStoragePathResolver.ResolveCompileRunArtifactsDirectory(
                    ResolveStorageRoot(),
                    projectFingerprint,
                    runId);
                return new CompileArtifactPaths(
                    ArtifactsDir: artifactsDir,
                    RequestJsonPath: Path.Combine(artifactsDir, UcliStoragePathNames.CompileRequestFileName),
                    SummaryJsonPath: Path.Combine(artifactsDir, UcliStoragePathNames.CompileSummaryFileName),
                    DiagnosticsJsonPath: Path.Combine(artifactsDir, UcliStoragePathNames.CompileDiagnosticsFileName));
            }

            private static string ResolveStorageRoot ()
            {
                return UcliStoragePathResolver.ResolveStorageRoot(UnityProjectPathResolver.ResolveProjectRootPath());
            }
        }
    }
}
