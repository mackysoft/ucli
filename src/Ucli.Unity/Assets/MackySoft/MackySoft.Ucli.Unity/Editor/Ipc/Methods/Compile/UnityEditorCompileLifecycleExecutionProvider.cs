using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Unity.Runtime;
using UnityEditor.Compilation;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Projects;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary>
    /// Adapts the current Unity Editor refresh and compilation callbacks to
    /// the compile lifecycle execution provider port.
    /// </summary>
    internal sealed class UnityEditorCompileLifecycleExecutionProvider :
        ICompileLifecycleExecutionProvider
    {
        private readonly IUnityEditorReadinessGate readinessGate;
        private readonly UnityProjectIdentity projectIdentity;
        private readonly IServerVersionProvider serverVersionProvider;
        private readonly IUnityMutationLaneControl mutationLaneControl;
        private readonly IUnityAssetRefreshController assetRefreshController;
        private readonly IUnityEditorUpdateAwaiter editorUpdateAwaiter;

        public UnityEditorCompileLifecycleExecutionProvider (
            IUnityEditorReadinessGate readinessGate,
            UnityProjectIdentity projectIdentity,
            IServerVersionProvider serverVersionProvider,
            IUnityMutationLaneControl mutationLaneControl,
            IUnityAssetRefreshController assetRefreshController,
            IUnityEditorUpdateAwaiter editorUpdateAwaiter)
        {
            this.readinessGate = readinessGate
                ?? throw new ArgumentNullException(nameof(readinessGate));
            this.projectIdentity = projectIdentity
                ?? throw new ArgumentNullException(nameof(projectIdentity));
            this.serverVersionProvider = serverVersionProvider
                ?? throw new ArgumentNullException(nameof(serverVersionProvider));
            this.mutationLaneControl = mutationLaneControl
                ?? throw new ArgumentNullException(nameof(mutationLaneControl));
            this.assetRefreshController = assetRefreshController
                ?? throw new ArgumentNullException(nameof(assetRefreshController));
            this.editorUpdateAwaiter = editorUpdateAwaiter
                ?? throw new ArgumentNullException(nameof(editorUpdateAwaiter));
        }

        public UnityEditorRuntimeObservation CaptureObservation ()
        {
            return readinessGate.CaptureObservation();
        }

        public UnityEditorObservation CreateLifecycleObservation (
            UnityEditorRuntimeObservation observation)
        {
            return UnityLifecycleResponseFactory.Create(
                projectIdentity,
                serverVersionProvider.GetVersion(),
                observation);
        }

        public CompileLifecycleResult.LifecycleEvidence CreateLifecycleEvidence (
            UnityEditorRuntimeObservation observation)
        {
            return new CompileLifecycleResult.LifecycleEvidence(
                serverVersionProvider.GetVersion(),
                projectIdentity.UnityVersion,
                observation.State,
                observation.ObservedAtUtc,
                observation.ActionRequired,
                observation.PrimaryDiagnostic);
        }

        public IUnityMutationActivity BeginMutation ()
        {
            return mutationLaneControl.BeginMutation();
        }

        public void RequestRefresh ()
        {
            assetRefreshController.Refresh();
        }

        public Task WaitForNextUpdateAsync (
            CancellationToken cancellationToken)
        {
            return editorUpdateAwaiter.WaitForNextUpdateAsync(cancellationToken);
        }

        public IDisposable BeginDiagnosticsObservation (
            ICompileLifecycleExecutionDiagnosticsSink diagnosticsSink)
        {
            return new DiagnosticsObservation(
                diagnosticsSink
                    ?? throw new ArgumentNullException(
                        nameof(diagnosticsSink)),
                () => readinessGate.CaptureObservation()
                    .State.Generations.CompileGeneration);
        }

        internal sealed class DiagnosticsObservation :
            IDisposable
        {
            private readonly ICompileLifecycleExecutionDiagnosticsSink
                diagnosticsSink;

            private readonly Func<long> compileGenerationProvider;

            private long? activeBatchId;

            private long? lastProcessedBatchId;

            private object activeContext;

            private object lastProcessedContext;

            public DiagnosticsObservation (
                ICompileLifecycleExecutionDiagnosticsSink diagnosticsSink,
                Func<long> compileGenerationProvider)
            {
                this.diagnosticsSink = diagnosticsSink;
                this.compileGenerationProvider = compileGenerationProvider
                    ?? throw new ArgumentNullException(
                        nameof(compileGenerationProvider));
                activeBatchId = diagnosticsSink.ActiveBatchId;
                lastProcessedBatchId =
                    diagnosticsSink.LastProcessedBatchId;
                CompilationPipeline.compilationStarted -= OnCompilationStarted;
                CompilationPipeline.assemblyCompilationFinished -=
                    OnAssemblyCompilationFinished;
                CompilationPipeline.compilationFinished -=
                    OnCompilationFinished;
                CompilationPipeline.compilationStarted += OnCompilationStarted;
                CompilationPipeline.assemblyCompilationFinished +=
                    OnAssemblyCompilationFinished;
                CompilationPipeline.compilationFinished +=
                    OnCompilationFinished;
            }

            public void Dispose ()
            {
                CompilationPipeline.compilationStarted -= OnCompilationStarted;
                CompilationPipeline.assemblyCompilationFinished -=
                    OnAssemblyCompilationFinished;
                CompilationPipeline.compilationFinished -=
                    OnCompilationFinished;
            }

            internal void OnCompilationStarted (object context)
            {
                if (Equals(activeContext, context)
                    || Equals(lastProcessedContext, context))
                {
                    return;
                }

                var observedBatchId = compileGenerationProvider();
                var batchId = lastProcessedBatchId.HasValue
                    && observedBatchId
                        <= lastProcessedBatchId.Value
                            ? checked(lastProcessedBatchId.Value + 1)
                            : observedBatchId;
                diagnosticsSink.StartBatch(batchId);
                activeBatchId = batchId;
                lastProcessedBatchId = null;
                activeContext = context;
                lastProcessedContext = null;
            }

            internal void OnAssemblyCompilationFinished (
                string assemblyPath,
                CompilerMessage[] messages)
            {
                var batchId = ResolveBatchIdForAssembly();
                var errorCount = 0;
                var warningCount = 0;
                UnityEditorPrimaryDiagnostic primaryDiagnostic = null;
                if (messages != null)
                {
                    for (var index = 0; index < messages.Length; index++)
                    {
                        var message = messages[index];
                        if (message.type == CompilerMessageType.Error)
                        {
                            errorCount++;
                            primaryDiagnostic ??= CreateDiagnostic(message);
                        }
                        else if (message.type
                            == CompilerMessageType.Warning)
                        {
                            warningCount++;
                        }
                    }
                }

                diagnosticsSink.RecordAssembly(
                    batchId,
                    assemblyPath,
                    errorCount,
                    warningCount,
                    primaryDiagnostic);
            }

            internal void OnCompilationFinished (object context)
            {
                if (lastProcessedContext != null
                    && Equals(lastProcessedContext, context))
                {
                    diagnosticsSink.CompleteBatch(
                        lastProcessedBatchId.Value);
                    return;
                }
                if (activeContext != null
                    && !Equals(activeContext, context))
                {
                    throw new IOException(
                        "Unity compilation-finished context does not match the active compile batch.");
                }

                var batchId = activeBatchId
                    ?? StartUnobservedBatch();
                diagnosticsSink.CompleteBatch(batchId);
                activeBatchId = null;
                lastProcessedBatchId = batchId;
                activeContext = null;
                lastProcessedContext = context;
            }

            private long ResolveBatchIdForAssembly ()
            {
                if (activeBatchId.HasValue)
                {
                    return activeBatchId.Value;
                }
                return StartUnobservedBatch();
            }

            private long StartUnobservedBatch ()
            {
                var observedBatchId = compileGenerationProvider();
                var batchId = lastProcessedBatchId.HasValue
                    && observedBatchId <= lastProcessedBatchId.Value
                        ? checked(lastProcessedBatchId.Value + 1)
                        : observedBatchId;
                diagnosticsSink.StartBatch(batchId);
                activeBatchId = batchId;
                return batchId;
            }

            private static UnityEditorPrimaryDiagnostic CreateDiagnostic (
                CompilerMessage message)
            {
                return new UnityEditorPrimaryDiagnostic(
                    Kind: UnityEditorPrimaryDiagnosticKind.Compiler,
                    Code: null,
                    File: string.IsNullOrWhiteSpace(message.file)
                        ? null
                        : message.file,
                    Line: message.line > 0 ? message.line : null,
                    Column: message.column > 0 ? message.column : null,
                    Message: string.IsNullOrWhiteSpace(message.message)
                        ? null
                        : message.message);
            }
        }
    }
}
