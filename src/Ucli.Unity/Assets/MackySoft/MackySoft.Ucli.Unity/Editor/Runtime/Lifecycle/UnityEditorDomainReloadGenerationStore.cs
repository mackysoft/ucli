using UnityEditor;

namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary> Persists opaque domain-reload generation values across Unity AppDomain reloads. </summary>
    internal static class UnityEditorDomainReloadGenerationStore
    {
        private const string SessionStateKey = "MackySoft.Ucli.Unity.Ipc.DomainReloadGeneration";

        /// <summary> Restores the last persisted domain-reload generation. </summary>
        /// <returns> The persisted domain-reload generation, or <c>0</c> when none is stored. </returns>
        public static int Restore ()
        {
            return SessionState.GetInt(SessionStateKey, 0);
        }

        /// <summary> Advances the persisted domain-reload generation from the current stored value. </summary>
        /// <returns> The incremented persisted generation value. </returns>
        public static int Advance ()
        {
            var nextValue = Restore() + 1;
            SessionState.SetInt(SessionStateKey, nextValue);
            return nextValue;
        }

        /// <summary> Advances the persisted domain-reload generation and returns the new value. </summary>
        /// <param name="minimumValue"> The minimum in-memory generation value that the next persisted generation must exceed. </param>
        /// <returns> The incremented persisted generation value. </returns>
        public static int Advance (int minimumValue)
        {
            var nextValue = System.Math.Max(Restore(), minimumValue) + 1;
            SessionState.SetInt(SessionStateKey, nextValue);
            return nextValue;
        }

        /// <summary> Stores one persisted generation value. </summary>
        /// <param name="value"> The persisted generation value to store. </param>
        internal static void SetPersistedValue (int value)
        {
            SessionState.SetInt(SessionStateKey, value);
        }
    }
}
