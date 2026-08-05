using UnityEditor;
using UnityEngine;

namespace MackySoft.Ucli.Unity.Recording.Recorder
{
    /// <summary> Registers the adapter only when the verified Recorder package range compiled this assembly. </summary>
    [InitializeOnLoad]
    internal static class UnityRecorderAdapterRegistration
    {
        static UnityRecorderAdapterRegistration ()
        {
            var adapter = new UnityRecorderGameViewRecordingAdapter();
            if (!GameViewRecordingAdapterRegistry.Shared.TryRegister(adapter, out var errorMessage))
            {
                Debug.LogError(errorMessage);
            }
        }
    }
}
