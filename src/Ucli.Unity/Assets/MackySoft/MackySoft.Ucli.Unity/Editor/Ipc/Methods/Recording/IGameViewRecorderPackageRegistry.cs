namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary>Reads the Recorder package registered by the current Unity Editor process.</summary>
    internal interface IGameViewRecorderPackageRegistry
    {
        /// <summary>Attempts to get the registered Recorder package version.</summary>
        bool TryGetRecorderPackageVersion (out string version);
    }
}
