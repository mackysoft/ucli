using System;
using UnityEditor;

namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary> Requests asset refreshes through the Unity Editor provider. </summary>
    internal sealed class UnityAssetRefreshController : IUnityAssetRefreshController
    {
        /// <inheritdoc />
        public void Refresh ()
        {
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            }
            catch (Exception exception)
            {
                throw new UnityAssetRefreshException(
                    "Unity rejected the asset refresh request.",
                    exception);
            }
        }
    }
}
