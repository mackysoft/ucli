using MackySoft.Ucli.Contracts.Ipc;
using UnityEditor;

namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary> Persists opaque Play Mode generation values across Unity AppDomain reloads. </summary>
    internal static class UnityEditorPlayModeGenerationStore
    {
        private const string SessionStateKey = "MackySoft.Ucli.Unity.Ipc.PlayModeGeneration";
        private const string StableStateSessionStateKey = "MackySoft.Ucli.Unity.Ipc.PlayModeStableState";

        /// <summary> Restores the last persisted Play Mode generation. </summary>
        /// <returns> The persisted Play Mode generation, or <c>0</c> when none is stored. </returns>
        public static int Restore ()
        {
            return SessionState.GetInt(SessionStateKey, 0);
        }

        /// <summary> Restores the last observed stable Play Mode state. </summary>
        /// <returns> The persisted stable Play Mode state, or <see langword="null" /> when none is stored. </returns>
        public static string RestoreStableState ()
        {
            var state = SessionState.GetString(StableStateSessionStateKey, string.Empty);
            return string.IsNullOrWhiteSpace(state) ? null : state;
        }

        /// <summary> Advances the persisted Play Mode generation and returns the new value. </summary>
        /// <param name="minimumValue"> The minimum in-memory generation value that the next persisted generation must exceed. </param>
        /// <returns> The incremented persisted generation value. </returns>
        public static int Advance (int minimumValue)
        {
            var nextValue = System.Math.Max(Restore(), minimumValue) + 1;
            SessionState.SetInt(SessionStateKey, nextValue);
            return nextValue;
        }

        /// <summary> Stores the last observed stable Play Mode state. </summary>
        /// <param name="state"> The stable Play Mode state value. </param>
        public static void SetStableState (string state)
        {
            if (!string.Equals(state, IpcPlayModeStateNames.Playing, System.StringComparison.Ordinal)
                && !string.Equals(state, IpcPlayModeStateNames.Stopped, System.StringComparison.Ordinal))
            {
                throw new System.ArgumentException("Stable Play Mode state must be playing or stopped.", nameof(state));
            }

            SessionState.SetString(StableStateSessionStateKey, state);
        }

        /// <summary> Stores one persisted Play Mode generation value. </summary>
        /// <param name="value"> The persisted generation value to store. </param>
        internal static void SetPersistedValue (int value)
        {
            SessionState.SetInt(SessionStateKey, value);
            SetPersistedStableState(null);
        }

        /// <summary> Stores one persisted stable Play Mode state value. </summary>
        /// <param name="state"> The stable state value to store, or <see langword="null" /> to clear it. </param>
        internal static void SetPersistedStableState (string state)
        {
            if (state == null)
            {
                SessionState.SetString(StableStateSessionStateKey, string.Empty);
                return;
            }

            SetStableState(state);
        }
    }
}
