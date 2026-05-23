using MackySoft.Ucli.Contracts.Ipc;
using UnityEditor;

namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary> Provides one access boundary for Unity <see cref="SessionState" /> values used by the daemon runtime. </summary>
    internal static class UnityEditorSessionStateStore
    {
        // NOTE: Unity SessionState survives domain reload but not process exit.
        // Keeping all keys and direct access here prevents recovery identity and
        // lifecycle counters from drifting through duplicate persistence rules.
        private const string DomainReloadGenerationKey = "MackySoft.Ucli.Unity.Ipc.DomainReloadGeneration";
        private const string PlayModeGenerationKey = "MackySoft.Ucli.Unity.Ipc.PlayModeGeneration";
        private const string PlayModeStableStateKey = "MackySoft.Ucli.Unity.Ipc.PlayModeStableState";
        private const string EditorInstanceIdKey = "MackySoft.Ucli.Unity.Runtime.EditorInstanceId";

        /// <summary> Gets the current Unity Editor process instance identifier. </summary>
        /// <returns> A non-empty identifier that remains stable until the Editor process exits. </returns>
        public static string GetOrCreateEditorInstanceId ()
        {
            var existingValue = SessionState.GetString(EditorInstanceIdKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(existingValue))
            {
                return existingValue;
            }

            var createdValue = System.Guid.NewGuid().ToString("N");
            SessionState.SetString(EditorInstanceIdKey, createdValue);
            return createdValue;
        }

        /// <summary> Replaces the current process instance identifier for tests. </summary>
        /// <param name="editorInstanceId"> The value to store in Unity <see cref="SessionState" />. </param>
        internal static void SetEditorInstanceIdForTests (string editorInstanceId)
        {
            SessionState.SetString(EditorInstanceIdKey, editorInstanceId ?? string.Empty);
        }

        /// <summary> Restores the last persisted domain-reload generation. </summary>
        /// <returns> The persisted domain-reload generation, or <c>0</c> when none is stored. </returns>
        public static int RestoreDomainReloadGeneration ()
        {
            return SessionState.GetInt(DomainReloadGenerationKey, 0);
        }

        /// <summary> Advances the persisted domain-reload generation and returns the new value. </summary>
        /// <param name="minimumValue"> The minimum in-memory generation value that the next persisted generation must exceed. </param>
        /// <returns> The incremented persisted generation value. </returns>
        public static int AdvanceDomainReloadGeneration (int minimumValue = 0)
        {
            return AdvanceGeneration(DomainReloadGenerationKey, RestoreDomainReloadGeneration(), minimumValue);
        }

        /// <summary> Stores one persisted domain-reload generation value for tests. </summary>
        /// <param name="value"> The persisted generation value to store. </param>
        internal static void SetDomainReloadGenerationForTests (int value)
        {
            SessionState.SetInt(DomainReloadGenerationKey, value);
        }

        /// <summary> Restores the last persisted Play Mode generation. </summary>
        /// <returns> The persisted Play Mode generation, or <c>0</c> when none is stored. </returns>
        public static int RestorePlayModeGeneration ()
        {
            return SessionState.GetInt(PlayModeGenerationKey, 0);
        }

        /// <summary> Restores the last observed stable Play Mode state. </summary>
        /// <returns> The persisted stable Play Mode state, or <see langword="null" /> when none is stored. </returns>
        public static IpcPlayModeState? RestorePlayModeStableState ()
        {
            var persistedState = SessionState.GetString(PlayModeStableStateKey, string.Empty);
            if (!IpcPlayModeStateCodec.TryParse(persistedState, out var state) || !IsStableState(state))
            {
                return null;
            }

            return state;
        }

        /// <summary> Advances the persisted Play Mode generation and returns the new value. </summary>
        /// <param name="minimumValue"> The minimum in-memory generation value that the next persisted generation must exceed. </param>
        /// <returns> The incremented persisted generation value. </returns>
        public static int AdvancePlayModeGeneration (int minimumValue)
        {
            return AdvanceGeneration(PlayModeGenerationKey, RestorePlayModeGeneration(), minimumValue);
        }

        /// <summary> Stores the last observed stable Play Mode state. </summary>
        /// <param name="state"> The stable Play Mode state value. </param>
        public static void SetPlayModeStableState (IpcPlayModeState state)
        {
            if (!IsStableState(state))
            {
                throw new System.ArgumentException("Stable Play Mode state must be playing or stopped.", nameof(state));
            }

            SessionState.SetString(PlayModeStableStateKey, IpcPlayModeStateCodec.ToValue(state));
        }

        /// <summary> Stores one persisted Play Mode generation value. </summary>
        /// <param name="value"> The persisted generation value to store. </param>
        internal static void SetPlayModeGenerationForTests (int value)
        {
            SessionState.SetInt(PlayModeGenerationKey, value);
            SetPlayModeStableStateForTests(null);
        }

        /// <summary> Stores one persisted stable Play Mode state value. </summary>
        /// <param name="state"> The stable state value to store, or <see langword="null" /> to clear it. </param>
        internal static void SetPlayModeStableStateForTests (IpcPlayModeState? state)
        {
            if (!state.HasValue)
            {
                SessionState.SetString(PlayModeStableStateKey, string.Empty);
                return;
            }

            SetPlayModeStableState(state.Value);
        }

        private static int AdvanceGeneration (
            string key,
            int persistedValue,
            int minimumValue)
        {
            var nextValue = System.Math.Max(persistedValue, minimumValue) + 1;
            SessionState.SetInt(key, nextValue);
            return nextValue;
        }

        private static bool IsStableState (IpcPlayModeState state)
        {
            return state is IpcPlayModeState.Playing or IpcPlayModeState.Stopped;
        }
    }
}
