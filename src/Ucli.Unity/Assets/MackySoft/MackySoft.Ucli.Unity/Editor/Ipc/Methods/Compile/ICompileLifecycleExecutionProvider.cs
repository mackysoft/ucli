using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Unity.Runtime;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary>
    /// Provides the compile action with Unity observations and side effects.
    /// A different compile pipeline can replace this port without taking
    /// ownership of lifecycle state, checkpoints, or terminal publication.
    /// </summary>
    internal interface ICompileLifecycleExecutionProvider
    {
        UnityEditorRuntimeObservation CaptureObservation ();

        UnityEditorObservation CreateLifecycleObservation (
            UnityEditorRuntimeObservation observation);

        CompileLifecycleResult.LifecycleEvidence CreateLifecycleEvidence (
            UnityEditorRuntimeObservation observation);

        IUnityMutationActivity BeginMutation ();

        void RequestRefresh ();

        Task WaitForNextUpdateAsync (CancellationToken cancellationToken);

        IDisposable BeginDiagnosticsObservation (
            ICompileLifecycleExecutionDiagnosticsSink diagnosticsSink);
    }

    /// <summary>
    /// Persists compile callback evidence before a domain reload can replace
    /// the provider instance that observed it.
    /// </summary>
    internal interface ICompileLifecycleExecutionDiagnosticsSink
    {
        long? ActiveBatchId { get; }

        long? LastProcessedBatchId { get; }

        void StartBatch (long batchId);

        void RecordAssembly (
            long batchId,
            string assemblyIdentity,
            int errorCount,
            int warningCount,
            UnityEditorPrimaryDiagnostic primaryDiagnostic);

        void CompleteBatch (long batchId);
    }
}
