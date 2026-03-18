namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Provides the version string of the running uCLI Unity plugin assembly. </summary>
    internal interface IServerVersionProvider
    {
        /// <summary> Gets the resolved uCLI Unity plugin version. </summary>
        /// <returns> The resolved version string. </returns>
        string GetVersion ();
    }
}