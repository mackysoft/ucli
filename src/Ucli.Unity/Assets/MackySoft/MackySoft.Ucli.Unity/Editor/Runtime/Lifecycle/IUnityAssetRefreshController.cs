namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary> Defines the Unity provider boundary that requests one synchronous asset refresh. </summary>
    internal interface IUnityAssetRefreshController
    {
        /// <summary> Requests a forced asset refresh from the Unity Editor. </summary>
        void Refresh ();
    }
}
