namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary> Requests Unity Editor Play Mode transitions. </summary>
    internal interface IUnityPlayModeController
    {
        /// <summary> Requests entering Play Mode. </summary>
        void EnterPlayMode ();

        /// <summary> Requests exiting Play Mode. </summary>
        void ExitPlayMode ();
    }
}
