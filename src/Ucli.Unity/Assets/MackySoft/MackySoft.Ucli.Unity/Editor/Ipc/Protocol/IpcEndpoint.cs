namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Represents one IPC endpoint consumed by Unity IPC server. </summary>
    internal sealed class IpcEndpoint
    {
        /// <summary> Initializes a new instance of the <see cref="IpcEndpoint" /> class. </summary>
        /// <param name="transportKind"> The transport kind used by endpoint binding. </param>
        /// <param name="address"> The transport-specific bind address. </param>
        public IpcEndpoint (
            IpcTransportKind transportKind,
            string address)
        {
            TransportKind = transportKind;
            Address = address;
        }

        /// <summary> Gets the transport kind used by endpoint binding. </summary>
        public IpcTransportKind TransportKind { get; }

        /// <summary> Gets the transport-specific bind address. </summary>
        public string Address { get; }
    }
}
