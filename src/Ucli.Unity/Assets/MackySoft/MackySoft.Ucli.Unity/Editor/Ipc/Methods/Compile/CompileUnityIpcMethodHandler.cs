using System;
using System.IO;
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
                var summary = await recorder.ExecuteAsync(cancellationToken).ConfigureAwait(false);
                var response = new IpcCompileResponse(
                    RunId: compileRequest.RunId,
                    SummaryJsonPath: paths.SummaryJsonPath,
                    DiagnosticsJsonPath: paths.DiagnosticsJsonPath,
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
                var compileArtifactsDirectory = CompileArtifactPaths.ResolveCompileArtifactsDirectory(projectIdentity.ProjectFingerprint);
                if (!Directory.Exists(compileArtifactsDirectory))
                {
                    return;
                }

                foreach (var runDirectoryPath in Directory.EnumerateDirectories(compileArtifactsDirectory))
                {
                    var summaryPath = Path.Combine(runDirectoryPath, UcliStoragePathNames.CompileSummaryFileName);
                    if (File.Exists(summaryPath))
                    {
                        continue;
                    }

                    var requestPath = Path.Combine(runDirectoryPath, UcliStoragePathNames.CompileRequestFileName);
                    if (!File.Exists(requestPath))
                    {
                        continue;
                    }

                    var pendingSummary = JsonSerializer.Deserialize<IpcCompileSummary>(
                        File.ReadAllText(requestPath),
                        IpcJsonSerializerOptions.Default);
                    if (pendingSummary == null || pendingSummary.Completed)
                    {
                        continue;
                    }

                    var paths = new CompileArtifactPaths(
                        runDirectoryPath,
                        requestPath,
                        summaryPath,
                        Path.Combine(runDirectoryPath, UcliStoragePathNames.CompileDiagnosticsFileName));
                    var finalSummary = CreateFinalSummary(
                        pendingSummary,
                        readinessGate.CaptureSnapshot(),
                        serverVersionProvider,
                        DateTimeOffset.UtcNow,
                        diagnostics: new DiagnosticAccumulator());
                    WriteDiagnostics(paths.DiagnosticsJsonPath, finalSummary);
                    WriteJsonAtomically(paths.SummaryJsonPath, finalSummary);
                }
            }
            catch (Exception exception)
            {
                daemonLogger.Warning(
                    DaemonLogCategories.Ipc,
                    $"Compile pending-run recovery skipped. {exception.Message}");
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

        private static async Task WaitUntilCompileSettledAsync (
            IUnityEditorReadinessGate readinessGate,
            CancellationToken cancellationToken)
        {
            var stableUpdates = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var snapshot = readinessGate.CaptureSnapshot();
                if (IsLifecycleSettled(snapshot))
                {
                    stableUpdates++;
                    if (stableUpdates >= 2)
                    {
                        return;
                    }
                }
                else
                {
                    stableUpdates = 0;
                }

                await WaitForNextEditorUpdateAsync(cancellationToken).ConfigureAwait(false);
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
            var tempPath = path + ".tmp";
            File.WriteAllText(
                tempPath,
                JsonSerializer.Serialize(value, IpcJsonSerializerOptions.Default));
            FileSystemAccessBoundary.EnsureSecureFile(tempPath);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            File.Move(tempPath, path);
            FileSystemAccessBoundary.EnsureSecureFile(path);
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
                    await WaitUntilCompileSettledAsync(readinessGate, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    Dispose();
                }

                var completedAtUtc = DateTimeOffset.UtcNow;
                var finalSummary = CreateFinalSummary(
                    pendingSummary,
                    readinessGate.CaptureSnapshot(),
                    serverVersionProvider,
                    completedAtUtc,
                    diagnostics);
                WriteDiagnostics(paths.DiagnosticsJsonPath, finalSummary);
                WriteJsonAtomically(paths.SummaryJsonPath, finalSummary);
                return finalSummary;
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
                    Code: null,
                    File: string.IsNullOrWhiteSpace(message.file) ? null : message.file,
                    Line: message.line > 0 ? message.line : null,
                    Column: message.column > 0 ? message.column : null,
                    Message: string.IsNullOrWhiteSpace(message.message) ? null : message.message.Trim());
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
