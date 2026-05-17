using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Unity.Project;
using MackySoft.Ucli.Unity.Runtime;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Handles <c>compile</c> IPC method requests. </summary>
    internal sealed class CompileUnityIpcMethodHandler : IUnityIpcMethodHandler
    {
        private const string RefreshOriginAssetDatabaseRefresh = "assetDatabaseRefresh";
        private const long MaxCompileRequestBytes = 1024 * 1024;
        private const int RequiredStableLifecycleObservations = 2;

        private readonly IUnityEditorReadinessGate readinessGate;

        private readonly IpcProjectIdentity projectIdentity;

        private readonly IServerVersionProvider serverVersionProvider;

        private readonly IDaemonLogger daemonLogger;

        /// <summary> Initializes a new instance of the <see cref="CompileUnityIpcMethodHandler" /> class. </summary>
        public CompileUnityIpcMethodHandler (
            IUnityEditorReadinessGate readinessGate,
            IpcProjectIdentity projectIdentity,
            IServerVersionProvider serverVersionProvider,
            IDaemonLogger daemonLogger = null)
        {
            this.readinessGate = readinessGate ?? throw new ArgumentNullException(nameof(readinessGate));
            this.projectIdentity = projectIdentity ?? throw new ArgumentNullException(nameof(projectIdentity));
            this.serverVersionProvider = serverVersionProvider ?? throw new ArgumentNullException(nameof(serverVersionProvider));
            this.daemonLogger = daemonLogger ?? NoOpDaemonLogger.Instance;
            RecoverPendingRuns(
                this.readinessGate,
                this.projectIdentity,
                this.serverVersionProvider,
                this.daemonLogger);
        }

        /// <inheritdoc />
        public string Method => IpcMethodNames.Compile;

        /// <inheritdoc />
        public async ValueTask<IpcResponse> HandleAsync (
            IpcRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!UnityIpcRequestCodec.TryDecodeCompileRequest(
                    request,
                    out IpcCompileRequest? compileRequest,
                    out var errorResponse))
            {
                daemonLogger.Warning(
                    DaemonLogCategories.Ipc,
                    "Compile payload decode failed.");
                return errorResponse!;
            }

            if (!IsValidRunId(compileRequest!.RunId))
            {
                return UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    UcliCoreErrorCodes.InvalidArgument,
                    "Compile runId must be one non-empty path segment.",
                    null);
            }

            try
            {
                var paths = CompileArtifactPaths.Resolve(projectIdentity.ProjectFingerprint, compileRequest.RunId);
                var recorder = new CompileRunRecorder(
                    compileRequest.RunId,
                    projectIdentity,
                    readinessGate,
                    serverVersionProvider,
                    paths);
                // NOTE: Compile handling observes Unity Editor APIs across awaits, so it must remain on
                // Unity's synchronization context instead of resuming on a thread-pool thread.
                var summary = await recorder.ExecuteAsync(cancellationToken);
                var response = new IpcCompileResponse(
                    RunId: compileRequest.RunId,
                    Summary: summary);
                return UnityIpcResponseFactory.CreateSuccessResponse(request, response);
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

        private static void RecoverPendingRuns (
            IUnityEditorReadinessGate readinessGate,
            IpcProjectIdentity projectIdentity,
            IServerVersionProvider serverVersionProvider,
            IDaemonLogger daemonLogger)
        {
            try
            {
                if (TryRecoverPendingRuns(
                    readinessGate,
                    projectIdentity,
                    serverVersionProvider,
                    daemonLogger))
                {
                    return;
                }

                SchedulePendingRunRecovery(
                    readinessGate,
                    projectIdentity,
                    serverVersionProvider,
                    daemonLogger);
            }
            catch (Exception exception)
            {
                daemonLogger.Warning(
                    DaemonLogCategories.Ipc,
                    $"Compile pending-run recovery skipped. {exception.Message}");
            }
        }

        private static bool TryRecoverPendingRuns (
            IUnityEditorReadinessGate readinessGate,
            IpcProjectIdentity projectIdentity,
            IServerVersionProvider serverVersionProvider,
            IDaemonLogger daemonLogger)
        {
            var compileArtifactsDirectory = CompileArtifactPaths.ResolveCompileArtifactsDirectory(projectIdentity.ProjectFingerprint);
            if (!Directory.Exists(compileArtifactsDirectory))
            {
                return true;
            }

            var hasDeferredPendingRun = false;
            FileSystemAccessBoundary.EnsureSecureDirectory(compileArtifactsDirectory);
            foreach (var runDirectoryPath in Directory.EnumerateDirectories(compileArtifactsDirectory))
            {
                try
                {
                    if (RecoverPendingRun(
                        runDirectoryPath,
                        readinessGate,
                        serverVersionProvider,
                        daemonLogger) == PendingRunRecoveryStatus.Deferred)
                    {
                        hasDeferredPendingRun = true;
                    }
                }
                catch (Exception exception)
                {
                    daemonLogger.Warning(
                        DaemonLogCategories.Ipc,
                        $"Compile pending-run recovery skipped for '{runDirectoryPath}'. {exception.Message}");
                }
            }

            return !hasDeferredPendingRun;
        }

        private static void SchedulePendingRunRecovery (
            IUnityEditorReadinessGate readinessGate,
            IpcProjectIdentity projectIdentity,
            IServerVersionProvider serverVersionProvider,
            IDaemonLogger daemonLogger)
        {
            var observationWindow = new SettledLifecycleObservationWindow();

            void RecoverOnSettledEditorUpdate ()
            {
                try
                {
                    var snapshot = readinessGate.CaptureSnapshot();
                    if (!observationWindow.Observe(snapshot))
                    {
                        return;
                    }

                    EditorApplication.update -= RecoverOnSettledEditorUpdate;
                    RecoverPendingRuns(
                        readinessGate,
                        projectIdentity,
                        serverVersionProvider,
                        daemonLogger);
                }
                catch (Exception exception)
                {
                    EditorApplication.update -= RecoverOnSettledEditorUpdate;
                    daemonLogger.Warning(
                        DaemonLogCategories.Ipc,
                        $"Compile pending-run delayed recovery skipped. {exception.Message}");
                }
            }

            EditorApplication.update += RecoverOnSettledEditorUpdate;
        }

        private static PendingRunRecoveryStatus RecoverPendingRun (
            string runDirectoryPath,
            IUnityEditorReadinessGate readinessGate,
            IServerVersionProvider serverVersionProvider,
            IDaemonLogger daemonLogger)
        {
            FileSystemAccessBoundary.EnsureSecureDirectory(runDirectoryPath);
            var summaryPath = Path.Combine(runDirectoryPath, UcliStoragePathNames.CompileSummaryFileName);
            if (File.Exists(summaryPath))
            {
                return PendingRunRecoveryStatus.Skipped;
            }

            var requestPath = Path.Combine(runDirectoryPath, UcliStoragePathNames.CompileRequestFileName);
            if (!TryReadPendingSummary(requestPath, daemonLogger, out var pendingSummary)
                || pendingSummary == null
                || pendingSummary.Completed)
            {
                return PendingRunRecoveryStatus.Skipped;
            }

            var snapshot = readinessGate.CaptureSnapshot();
            if (!IsLifecycleSettled(snapshot))
            {
                return PendingRunRecoveryStatus.Deferred;
            }

            var paths = new CompileArtifactPaths(
                runDirectoryPath,
                requestPath,
                summaryPath,
                Path.Combine(runDirectoryPath, UcliStoragePathNames.CompileDiagnosticsFileName));
            var finalSummary = CreateFinalSummary(
                pendingSummary,
                snapshot,
                serverVersionProvider,
                DateTimeOffset.UtcNow,
                diagnostics: new DiagnosticAccumulator());
            WriteDiagnostics(paths.DiagnosticsJsonPath, finalSummary);
            WriteJsonAtomically(paths.SummaryJsonPath, finalSummary);
            return PendingRunRecoveryStatus.Completed;
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

        private static bool IsValidRunId (string runId)
        {
            return !string.IsNullOrWhiteSpace(runId)
                && runId.IndexOf('/') < 0
                && runId.IndexOf('\\') < 0
                && runId.IndexOf(':') < 0
                && !string.Equals(runId, ".", StringComparison.Ordinal)
                && !string.Equals(runId, "..", StringComparison.Ordinal);
        }

        private static IpcCompileSummary CreatePendingSummary (
            string runId,
            IpcProjectIdentity projectIdentity,
            UnityEditorLifecycleSnapshot beforeSnapshot,
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
                    Origin: RefreshOriginAssetDatabaseRefresh,
                    Requested: true,
                    StartedAtUtc: startedAtUtc,
                    CompletedAtUtc: null,
                    Completed: false),
                ScriptCompilation: new IpcCompileSummary.ScriptCompilationEvidence(
                    Started: false,
                    Completed: false,
                    CompileGenerationBefore: beforeSnapshot.CompileGeneration,
                    CompileGenerationAfter: beforeSnapshot.CompileGeneration,
                    Diagnostics: new IpcCompileSummary.DiagnosticsEvidence(0, 0, null)),
                DomainReload: new IpcCompileSummary.DomainReloadEvidence(
                    ReloadRequired: false,
                    ReloadObserved: false,
                    GenerationBefore: beforeSnapshot.DomainReloadGeneration,
                    GenerationAfter: beforeSnapshot.DomainReloadGeneration,
                    Settled: false),
                Lifecycle: CreateLifecycleEvidence(beforeSnapshot, projectIdentity, serverVersionProvider));
        }

        private static IpcCompileSummary CreateFinalSummary (
            IpcCompileSummary pendingSummary,
            UnityEditorLifecycleSnapshot afterSnapshot,
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

            var domainReloadObserved = !string.Equals(
                pendingSummary.DomainReload.GenerationBefore,
                afterSnapshot.DomainReloadGeneration,
                StringComparison.Ordinal);
            return pendingSummary with
            {
                Completed = true,
                CompletedAtUtc = completedAtUtc,
                Refresh = pendingSummary.Refresh with
                {
                    Completed = true,
                    CompletedAtUtc = completedAtUtc,
                },
                ScriptCompilation = pendingSummary.ScriptCompilation with
                {
                    Started = diagnostics.CompilationStarted || !string.Equals(
                        pendingSummary.ScriptCompilation.CompileGenerationBefore,
                        afterSnapshot.CompileGeneration,
                        StringComparison.Ordinal),
                    Completed = true,
                    CompileGenerationAfter = afterSnapshot.CompileGeneration,
                    Diagnostics = new IpcCompileSummary.DiagnosticsEvidence(
                        ErrorCount: errorCount,
                        WarningCount: diagnostics.WarningCount,
                        PrimaryDiagnostic: primaryDiagnostic),
                },
                DomainReload = pendingSummary.DomainReload with
                {
                    ReloadRequired = domainReloadObserved,
                    ReloadObserved = domainReloadObserved,
                    GenerationAfter = afterSnapshot.DomainReloadGeneration,
                    Settled = IsLifecycleSettled(afterSnapshot),
                },
                Lifecycle = CreateLifecycleEvidence(afterSnapshot, pendingSummary.ProjectFingerprint, serverVersionProvider),
            };
        }

        private static IpcCompileSummary.LifecycleEvidence CreateLifecycleEvidence (
            UnityEditorLifecycleSnapshot snapshot,
            IpcProjectIdentity projectIdentity,
            IServerVersionProvider serverVersionProvider)
        {
            return CreateLifecycleEvidence(snapshot, projectIdentity.ProjectFingerprint, serverVersionProvider);
        }

        private static IpcCompileSummary.LifecycleEvidence CreateLifecycleEvidence (
            UnityEditorLifecycleSnapshot snapshot,
            string projectFingerprint,
            IServerVersionProvider serverVersionProvider)
        {
            _ = projectFingerprint;
            return new IpcCompileSummary.LifecycleEvidence(
                ServerVersion: serverVersionProvider.GetVersion(),
                UnityVersion: Application.unityVersion,
                EditorMode: DaemonEditorModeCodec.ToValue(snapshot.EditorMode),
                LifecycleState: snapshot.LifecycleState,
                BlockingReason: snapshot.BlockingReason,
                CompileState: snapshot.CompileState,
                CompileGeneration: snapshot.CompileGeneration,
                DomainReloadGeneration: snapshot.DomainReloadGeneration,
                CanAcceptExecutionRequests: snapshot.CanAcceptExecutionRequests,
                ObservedAtUtc: snapshot.ObservedAtUtc,
                ActionRequired: snapshot.ActionRequired,
                PrimaryDiagnostic: snapshot.PrimaryDiagnostic);
        }

        private static bool IsLifecycleSettled (UnityEditorLifecycleSnapshot snapshot)
        {
            return !string.Equals(snapshot.LifecycleState, IpcEditorLifecycleStateCodec.DomainReloading, StringComparison.Ordinal)
                && !string.Equals(snapshot.LifecycleState, IpcEditorLifecycleStateCodec.Compiling, StringComparison.Ordinal)
                && !string.Equals(snapshot.LifecycleState, IpcEditorLifecycleStateCodec.Reimporting, StringComparison.Ordinal)
                && !string.Equals(snapshot.LifecycleState, IpcEditorLifecycleStateCodec.Recovering, StringComparison.Ordinal)
                && !string.Equals(snapshot.LifecycleState, IpcEditorLifecycleStateCodec.Starting, StringComparison.Ordinal);
        }

        private static async Task<UnityEditorLifecycleSnapshot> WaitUntilCompileSettledAsync (
            IUnityEditorReadinessGate readinessGate,
            CancellationToken cancellationToken)
        {
            var observationWindow = new SettledLifecycleObservationWindow();
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var snapshot = readinessGate.CaptureSnapshot();
                if (observationWindow.Observe(snapshot))
                {
                    return snapshot;
                }

                await WaitForNextEditorUpdateAsync(cancellationToken);
            }
        }

        private static Task WaitForNextEditorUpdateAsync (CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var completionSource = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            CancellationTokenRegistration registration = default;
            void Complete ()
            {
                EditorApplication.update -= Complete;
                registration.Dispose();
                completionSource.TrySetResult(null);
            }

            EditorApplication.update += Complete;
            if (cancellationToken.CanBeCanceled)
            {
                registration = cancellationToken.Register(static state =>
                {
                    var source = (TaskCompletionSource<object>)state;
                    source.TrySetCanceled();
                }, completionSource);
            }

            return completionSource.Task;
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

            private string stableLifecycleState;

            private string stableBlockingReason;

            private string stableCompileState;

            private string stableCompileGeneration;

            private string stableDomainReloadGeneration;

            private bool stableCanAcceptExecutionRequests;

            public bool Observe (UnityEditorLifecycleSnapshot snapshot)
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

            private bool MatchesStableSnapshot (UnityEditorLifecycleSnapshot snapshot)
            {
                return string.Equals(stableLifecycleState, snapshot.LifecycleState, StringComparison.Ordinal)
                    && string.Equals(stableBlockingReason, snapshot.BlockingReason, StringComparison.Ordinal)
                    && string.Equals(stableCompileState, snapshot.CompileState, StringComparison.Ordinal)
                    && string.Equals(stableCompileGeneration, snapshot.CompileGeneration, StringComparison.Ordinal)
                    && string.Equals(stableDomainReloadGeneration, snapshot.DomainReloadGeneration, StringComparison.Ordinal)
                    && stableCanAcceptExecutionRequests == snapshot.CanAcceptExecutionRequests;
            }

            private void CaptureStableSnapshot (UnityEditorLifecycleSnapshot snapshot)
            {
                hasStableSnapshot = true;
                stableLifecycleState = snapshot.LifecycleState;
                stableBlockingReason = snapshot.BlockingReason;
                stableCompileState = snapshot.CompileState;
                stableCompileGeneration = snapshot.CompileGeneration;
                stableDomainReloadGeneration = snapshot.DomainReloadGeneration;
                stableCanAcceptExecutionRequests = snapshot.CanAcceptExecutionRequests;
            }

            private void Reset ()
            {
                stableUpdates = 0;
                hasStableSnapshot = false;
                stableLifecycleState = null;
                stableBlockingReason = null;
                stableCompileState = null;
                stableCompileGeneration = null;
                stableDomainReloadGeneration = null;
                stableCanAcceptExecutionRequests = false;
            }
        }

        private enum PendingRunRecoveryStatus
        {
            Skipped,
            Completed,
            Deferred,
        }

        private sealed class CompileRunRecorder : IDisposable
        {
            private readonly string runId;

            private readonly IpcProjectIdentity projectIdentity;

            private readonly IUnityEditorReadinessGate readinessGate;

            private readonly IServerVersionProvider serverVersionProvider;

            private readonly CompileArtifactPaths paths;

            private readonly DiagnosticAccumulator diagnostics = new DiagnosticAccumulator();

            public CompileRunRecorder (
                string runId,
                IpcProjectIdentity projectIdentity,
                IUnityEditorReadinessGate readinessGate,
                IServerVersionProvider serverVersionProvider,
                CompileArtifactPaths paths)
            {
                this.runId = runId;
                this.projectIdentity = projectIdentity;
                this.readinessGate = readinessGate;
                this.serverVersionProvider = serverVersionProvider;
                this.paths = paths;
            }

            public async Task<IpcCompileSummary> ExecuteAsync (CancellationToken cancellationToken)
            {
                var beforeSnapshot = readinessGate.CaptureSnapshot();
                var startedAtUtc = DateTimeOffset.UtcNow;
                var pendingSummary = CreatePendingSummary(
                    runId,
                    projectIdentity,
                    beforeSnapshot,
                    serverVersionProvider,
                    startedAtUtc);
                WriteJsonAtomically(paths.RequestJsonPath, pendingSummary);

                Subscribe();
                try
                {
                    AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                    var afterSnapshot = await WaitUntilCompileSettledAsync(readinessGate, cancellationToken);
                    var completedAtUtc = DateTimeOffset.UtcNow;
                    var finalSummary = CreateFinalSummary(
                        pendingSummary,
                        afterSnapshot,
                        serverVersionProvider,
                        completedAtUtc,
                        diagnostics);
                    WriteDiagnostics(paths.DiagnosticsJsonPath, finalSummary);
                    WriteJsonAtomically(paths.SummaryJsonPath, finalSummary);
                    return finalSummary;
                }
                finally
                {
                    Dispose();
                }
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
                    Kind: "compiler",
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
                string projectFingerprint,
                string runId)
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

            public static string ResolveCompileArtifactsDirectory (string projectFingerprint)
            {
                return UcliStoragePathResolver.ResolveCompileArtifactsDirectory(
                    ResolveStorageRoot(),
                    projectFingerprint);
            }

            private static string ResolveStorageRoot ()
            {
                return UcliStoragePathResolver.ResolveStorageRoot(UnityProjectPathResolver.ResolveProjectRootPath());
            }
        }
    }
}
