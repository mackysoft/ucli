using System;
using UnityEditor;

namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary> Provides a Unity Editor process identifier that is stable across domain reloads. </summary>
    internal static class UnityEditorProcessIdentity
    {
        private const string EditorInstanceIdSessionStateKey = "MackySoft.Ucli.Unity.Runtime.EditorInstanceId";

        /// <summary> Gets the current Unity Editor process instance identifier. </summary>
        /// <returns> A non-empty identifier that remains stable until the Editor process exits. </returns>
        public static string GetEditorInstanceId ()
        {
            var existingValue = SessionState.GetString(EditorInstanceIdSessionStateKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(existingValue))
            {
                return existingValue;
            }

            var createdValue = Guid.NewGuid().ToString("N");
            SessionState.SetString(EditorInstanceIdSessionStateKey, createdValue);
            return createdValue;
        }

        /// <summary> Replaces the current process instance identifier for tests. </summary>
        /// <param name="editorInstanceId"> The value to store in Unity <see cref="SessionState" />. </param>
        internal static void SetEditorInstanceIdForTests (string editorInstanceId)
        {
            SessionState.SetString(EditorInstanceIdSessionStateKey, editorInstanceId ?? string.Empty);
        }
    }
}
