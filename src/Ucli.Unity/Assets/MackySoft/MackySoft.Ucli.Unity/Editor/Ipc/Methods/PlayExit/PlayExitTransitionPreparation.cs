using System;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary>
    /// Carries either a terminal pre-side-effect result or the observation that must be persisted before exit is issued.
    /// </summary>
    internal sealed record PlayExitTransitionPreparation
    {
        private PlayExitTransitionPreparation (
            UnityEditorObservation before,
            PlayExitTransitionExecutionResult terminalResult)
        {
            if ((before == null) == (terminalResult == null))
            {
                throw new ArgumentException(
                    "Play Mode exit preparation must carry exactly one outcome.");
            }

            Before = before;
            TerminalResult = terminalResult;
        }

        /// <summary> Gets the observation to persist before issuing the exit side effect. </summary>
        public UnityEditorObservation Before { get; }

        /// <summary> Gets the result established without issuing the exit side effect. </summary>
        public PlayExitTransitionExecutionResult TerminalResult { get; }

        /// <summary> Gets a value indicating whether the handler must durably admit the exit side effect. </summary>
        public bool RequiresSideEffect => Before != null;

        /// <summary> Creates a preparation that requires durable side-effect admission. </summary>
        public static PlayExitTransitionPreparation Issue (
            UnityEditorObservation before)
        {
            return new PlayExitTransitionPreparation(
                before ?? throw new ArgumentNullException(nameof(before)),
                terminalResult: null);
        }

        /// <summary> Creates a preparation that is already terminal without a side effect. </summary>
        public static PlayExitTransitionPreparation Terminal (
            PlayExitTransitionExecutionResult result)
        {
            return new PlayExitTransitionPreparation(
                before: null,
                result ?? throw new ArgumentNullException(nameof(result)));
        }
    }
}
