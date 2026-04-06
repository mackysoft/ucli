using UnityEditor;

namespace MackySoft.Ucli.Unity.Ipc
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

        /// <summary> Advances the persisted domain-reload generation and returns the new value. </summary>
        /// <param name="currentValue"> The current in-memory generation value. </param>
        /// <returns> The incremented persisted generation value. </returns>
        public static int Advance (int currentValue)
        {
            var nextValue = System.Math.Max(Restore(), currentValue) + 1;
            SessionState.SetInt(SessionStateKey, nextValue);
            return nextValue;
        }

        /// <summary> Sets one persisted generation value for test isolation. </summary>
        /// <param name="value"> The persisted generation value to store. </param>
        internal static void SetForTests (int value)
        {
            SessionState.SetInt(SessionStateKey, value);
        }
    }
}
