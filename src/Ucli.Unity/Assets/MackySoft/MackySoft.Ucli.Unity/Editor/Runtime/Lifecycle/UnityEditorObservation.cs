using System;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary> Represents one normalized Unity Editor observation used by IPC responses and readiness gates. </summary>
    internal sealed record UnityEditorObservation
    {
        /// <summary> Initializes one normalized Unity Editor observation. </summary>
        public UnityEditorObservation (
            UnityEditorStateSnapshot state,
            DateTimeOffset observedAtUtc,
            IpcPrimaryDiagnostic primaryDiagnostic = null)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            ObservedAtUtc = ContractArgumentGuard.RequireUtcTimestamp(observedAtUtc, nameof(observedAtUtc));
            PrimaryDiagnostic = primaryDiagnostic;
        }

        /// <summary> Gets the comparable Unity Editor state. </summary>
        public UnityEditorStateSnapshot State { get; }

        /// <summary> Gets the UTC timestamp when the state was observed. </summary>
        public DateTimeOffset ObservedAtUtc { get; }

        /// <summary> Gets the primary diagnostic associated with the observed state. </summary>
        public IpcPrimaryDiagnostic PrimaryDiagnostic { get; }

        /// <summary> Creates a copy with one lifecycle-state override while preserving the observed subsystem state. </summary>
        public UnityEditorObservation WithLifecycleState (IpcEditorLifecycleState lifecycleState)
        {
            return new UnityEditorObservation(
                state: new UnityEditorStateSnapshot(
                    editorMode: State.EditorMode,
                    lifecycleState: lifecycleState,
                    compileState: State.CompileState,
                    generations: State.Generations,
                    playMode: State.PlayMode),
                observedAtUtc: ObservedAtUtc,
                primaryDiagnostic: PrimaryDiagnostic);
        }

        /// <summary> Creates a copy recorded at the specified UTC observation time. </summary>
        public UnityEditorObservation WithObservedAtUtc (DateTimeOffset observedAtUtc)
        {
            return new UnityEditorObservation(State, observedAtUtc, PrimaryDiagnostic);
        }

        /// <summary> Gets the blocking reason derived from the lifecycle state. </summary>
        public IpcEditorBlockingReason? BlockingReason =>
            IpcEditorLifecycleSemantics.ResolveBlockingReason(State.LifecycleState);

        /// <summary> Gets a value indicating whether normal execution requests may be accepted. </summary>
        public bool CanAcceptExecutionRequests =>
            IpcEditorLifecycleSemantics.CanAcceptExecutionRequests(State.LifecycleState);

        /// <summary> Gets the action required to resolve the lifecycle state, when one is known. </summary>
        public DaemonDiagnosisActionRequired? ActionRequired => UnityEditorExecutionReadinessPolicy.ResolveActionRequired(State.LifecycleState);
    }
}
