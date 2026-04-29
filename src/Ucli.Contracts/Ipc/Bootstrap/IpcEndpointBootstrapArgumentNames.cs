namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines command-line argument names that carry IPC endpoint values for Unity batchmode hosts. </summary>
public static class IpcEndpointBootstrapArgumentNames
{
    /// <summary> Gets the argument name that carries endpoint transport kind literal. </summary>
    public const string TransportKind = "-ucliEndpointTransportKind";

    /// <summary> Gets the argument name that carries endpoint address. </summary>
    public const string Address = "-ucliEndpointAddress";
}
