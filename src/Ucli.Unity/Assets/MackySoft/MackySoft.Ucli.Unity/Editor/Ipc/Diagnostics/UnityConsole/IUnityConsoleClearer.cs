namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Clears the visible Unity Editor Console display. </summary>
    internal interface IUnityConsoleClearer
    {
        /// <summary> Clears the Unity Editor Console display. </summary>
        /// <returns> The clear operation result. </returns>
        UnityConsoleClearResult Clear ();
    }
}
