using UnityEditor;

namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary> Persists opaque Play Mode generation values across Unity AppDomain reloads. </summary>
    internal static class UnityEditorPlayModeGenerationStore
    {
        private const string SessionStateKey = "MackySoft.Ucli.Unity.Ipc.PlayModeGeneration";

        /// <summary> Restores the last persisted Play Mode generation. </summary>
        /// <returns> The persisted Play Mode generation, or <c>0</c> when none is stored. </returns>
        public static int Restore ()
        {
            return SessionState.GetInt(SessionStateKey, 0);
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

        /// <summary> Stores one persisted Play Mode generation value. </summary>
        /// <param name="value"> The persisted generation value to store. </param>
        internal static void SetPersistedValue (int value)
        {
            SessionState.SetInt(SessionStateKey, value);
        }
    }
}
