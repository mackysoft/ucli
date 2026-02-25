namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Defines transport kinds supported by Unity IPC server endpoints. </summary>
    internal enum IpcTransportKind
    {
        /// <summary> Uses Windows named pipe transport. </summary>
        NamedPipe = 0,

        /// <summary> Uses Unix domain socket transport. </summary>
        UnixDomainSocket = 1,
    }
}
