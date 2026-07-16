using System.Globalization;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using UnityEditor;

namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary> Provides one access boundary for Unity <see cref="SessionState" /> values used by the daemon runtime. </summary>
    internal static class UnityEditorSessionStateStore
    {
        // NOTE: Unity SessionState survives domain reload but not process exit.
        // Keeping all keys and direct access here prevents recovery identity and
        // lifecycle counters from drifting through duplicate persistence rules.
        private const string AssetRefreshGenerationKey = "MackySoft.Ucli.Unity.Ipc.AssetRefreshGeneration";
        private const string CompileGenerationKey = "MackySoft.Ucli.Unity.Ipc.CompileGeneration";
        private const string DomainReloadGenerationKey = "MackySoft.Ucli.Unity.Ipc.DomainReloadGeneration";
        private const string PlayModeGenerationKey = "MackySoft.Ucli.Unity.Ipc.PlayModeGeneration";
        private const string PlayModeStableStateKey = "MackySoft.Ucli.Unity.Ipc.PlayModeStableState";
        private const string EditorInstanceIdKey = "MackySoft.Ucli.Unity.Runtime.EditorInstanceId";
        private const string ScreenshotResolutionLeaseRegistryKey =
            "MackySoft.Ucli.ScreenshotCapture.TemporaryGameViewResolutions";

        /// <summary> Gets the current Unity Editor process instance identifier. </summary>
        /// <returns> A non-empty identifier that remains stable until the Editor process exits. </returns>
        public static System.Guid GetOrCreateEditorInstanceId ()
        {
            var existingValue = SessionState.GetString(EditorInstanceIdKey, string.Empty);
            if (existingValue.Length == 32
                && System.Guid.TryParseExact(existingValue, "N", out var existingEditorInstanceId)
                && existingEditorInstanceId != System.Guid.Empty)
            {
                return existingEditorInstanceId;
            }

            var createdEditorInstanceId = System.Guid.NewGuid();
            SessionState.SetString(EditorInstanceIdKey, createdEditorInstanceId.ToString("N"));
            return createdEditorInstanceId;
        }

        /// <summary> Replaces the current process instance identifier for tests. </summary>
        /// <param name="editorInstanceId"> The value to store in Unity <see cref="SessionState" />. </param>
        internal static void SetEditorInstanceIdForTests (string editorInstanceId)
        {
            SessionState.SetString(EditorInstanceIdKey, editorInstanceId ?? string.Empty);
        }

        /// <summary> Restores the last persisted compile generation. </summary>
        /// <returns> The persisted compile generation, or <c>0</c> when none is stored. </returns>
        public static long RestoreCompileGeneration ()
        {
            return RestoreGeneration(CompileGenerationKey);
        }

        /// <summary> Advances the persisted compile generation and returns the new value. </summary>
        /// <param name="minimumValue"> The minimum in-memory generation value that the next persisted generation must exceed. </param>
        /// <returns> The incremented persisted generation value. </returns>
        public static long AdvanceCompileGeneration (long minimumValue = 0)
        {
            return AdvanceGeneration(CompileGenerationKey, RestoreCompileGeneration(), minimumValue);
        }

        /// <summary> Stores one persisted compile generation value for tests. </summary>
        /// <param name="value"> The persisted compile generation value to store. </param>
        internal static void SetCompileGenerationForTests (long value)
        {
            PersistGeneration(CompileGenerationKey, value);
        }

        /// <summary> Restores the last persisted domain-reload generation. </summary>
        /// <returns> The persisted domain-reload generation, or <c>0</c> when none is stored. </returns>
        public static long RestoreDomainReloadGeneration ()
        {
            return RestoreGeneration(DomainReloadGenerationKey);
        }

        /// <summary> Advances the persisted domain-reload generation and returns the new value. </summary>
        /// <param name="minimumValue"> The minimum in-memory generation value that the next persisted generation must exceed. </param>
        /// <returns> The incremented persisted generation value. </returns>
        public static long AdvanceDomainReloadGeneration (long minimumValue = 0)
        {
            return AdvanceGeneration(DomainReloadGenerationKey, RestoreDomainReloadGeneration(), minimumValue);
        }

        /// <summary> Stores one persisted domain-reload generation value for tests. </summary>
        /// <param name="value"> The persisted generation value to store. </param>
        internal static void SetDomainReloadGenerationForTests (long value)
        {
            PersistGeneration(DomainReloadGenerationKey, value);
        }

        /// <summary> Restores the last persisted asset-refresh generation. </summary>
        /// <returns> The persisted asset-refresh generation, or <c>0</c> when none is stored. </returns>
        public static long RestoreAssetRefreshGeneration ()
        {
            return RestoreGeneration(AssetRefreshGenerationKey);
        }

        /// <summary> Advances the persisted asset-refresh generation and returns the new value. </summary>
        /// <param name="minimumValue"> The minimum in-memory generation value that the next persisted generation must exceed. </param>
        /// <returns> The incremented persisted generation value. </returns>
        public static long AdvanceAssetRefreshGeneration (long minimumValue = 0)
        {
            return AdvanceGeneration(AssetRefreshGenerationKey, RestoreAssetRefreshGeneration(), minimumValue);
        }

        /// <summary> Stores one persisted asset-refresh generation value for tests. </summary>
        /// <param name="value"> The persisted generation value to store. </param>
        internal static void SetAssetRefreshGenerationForTests (long value)
        {
            PersistGeneration(AssetRefreshGenerationKey, value);
        }

        /// <summary> Restores the last persisted Play Mode generation. </summary>
        /// <returns> The persisted Play Mode generation, or <c>0</c> when none is stored. </returns>
        public static long RestorePlayModeGeneration ()
        {
            return RestoreGeneration(PlayModeGenerationKey);
        }

        /// <summary> Restores the last observed stable Play Mode state. </summary>
        /// <returns> The persisted stable Play Mode state, or <see langword="null" /> when none is stored. </returns>
        public static IpcPlayModeState? RestorePlayModeStableState ()
        {
            var persistedState = SessionState.GetString(PlayModeStableStateKey, string.Empty);
            if (!ContractLiteralInputParser.TryParseTrimmed<IpcPlayModeState>(persistedState, out var state) || !IsStableState(state))
            {
                return null;
            }

            return state;
        }

        /// <summary> Advances the persisted Play Mode generation and returns the new value. </summary>
        /// <param name="minimumValue"> The minimum in-memory generation value that the next persisted generation must exceed. </param>
        /// <returns> The incremented persisted generation value. </returns>
        public static long AdvancePlayModeGeneration (long minimumValue)
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

            SessionState.SetString(PlayModeStableStateKey, ContractLiteralCodec.ToValue(state));
        }

        /// <summary> Stores one persisted Play Mode generation value. </summary>
        /// <param name="value"> The persisted generation value to store. </param>
        internal static void SetPlayModeGenerationForTests (long value)
        {
            PersistGeneration(PlayModeGenerationKey, value);
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

        /// <summary> Restores the serialized screenshot resolution lease registry. </summary>
        public static string RestoreScreenshotResolutionLeaseRegistry ()
        {
            return SessionState.GetString(ScreenshotResolutionLeaseRegistryKey, string.Empty);
        }

        /// <summary> Persists the serialized screenshot resolution lease registry. </summary>
        public static void PersistScreenshotResolutionLeaseRegistry (string serializedRegistry)
        {
            SessionState.SetString(ScreenshotResolutionLeaseRegistryKey, serializedRegistry ?? string.Empty);
        }

        /// <summary> Clears the screenshot resolution lease registry for tests. </summary>
        internal static void ClearScreenshotResolutionLeaseRegistryForTests ()
        {
            SessionState.EraseString(ScreenshotResolutionLeaseRegistryKey);
        }

        private static long AdvanceGeneration (
            string key,
            long persistedValue,
            long minimumValue)
        {
            var nextValue = checked(System.Math.Max(persistedValue, minimumValue) + 1L);
            PersistGeneration(key, nextValue);
            return nextValue;
        }

        private static long RestoreGeneration (string key)
        {
            var persistedValue = SessionState.GetString(key, string.Empty);
            return long.TryParse(
                    persistedValue,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var generation)
                && generation >= 0
                    ? generation
                    : 0L;
        }

        private static void PersistGeneration (string key, long value)
        {
            if (value < 0)
            {
                throw new System.ArgumentOutOfRangeException(nameof(value), value, "Generation must not be negative.");
            }

            SessionState.SetString(key, value.ToString(CultureInfo.InvariantCulture));
        }

        private static bool IsStableState (IpcPlayModeState state)
        {
            return state is IpcPlayModeState.Playing or IpcPlayModeState.Stopped;
        }
    }
}
