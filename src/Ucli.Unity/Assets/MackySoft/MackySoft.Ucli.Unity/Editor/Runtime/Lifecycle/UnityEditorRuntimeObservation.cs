using System;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary> Represents one Unity Editor runtime state snapshot used by local readiness and action decisions. </summary>
    internal sealed record UnityEditorRuntimeObservation
    {
        /// <summary> Initializes one Unity Editor runtime state snapshot. </summary>
        public UnityEditorRuntimeObservation (
            UnityEditorStateSnapshot state,
            DateTimeOffset observedAtUtc,
            UnityEditorPrimaryDiagnostic primaryDiagnostic = null)
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
        public UnityEditorPrimaryDiagnostic PrimaryDiagnostic { get; }

        /// <summary> Creates a copy with one lifecycle-state override while preserving the observed subsystem state. </summary>
        public UnityEditorRuntimeObservation WithLifecycleState (UnityEditorLifecycleState lifecycleState)
        {
            return new UnityEditorRuntimeObservation(
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
        public UnityEditorRuntimeObservation WithObservedAtUtc (DateTimeOffset observedAtUtc)
        {
            return new UnityEditorRuntimeObservation(State, observedAtUtc, PrimaryDiagnostic);
        }

        /// <summary> Gets the blocking reason derived from the lifecycle state. </summary>
        public UnityEditorBlockingReason? BlockingReason =>
            UnityEditorLifecycleSemantics.ResolveBlockingReason(State.LifecycleState);

        /// <summary> Gets a value indicating whether normal execution requests may be accepted. </summary>
        public bool CanAcceptExecutionRequests =>
            UnityEditorLifecycleSemantics.CanAcceptExecutionRequests(State.LifecycleState);

        /// <summary> Gets the action required to resolve the lifecycle state, when one is known. </summary>
        public UnityEditorActionRequired? ActionRequired => UnityEditorExecutionReadinessPolicy.ResolveActionRequired(State.LifecycleState);
    }
}
