namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines command-line argument names used to bootstrap Unity IPC daemon mode. </summary>
public static class IpcDaemonBootstrapArgumentNames
{
    /// <summary> Gets the argument name that carries repository root path. </summary>
    public const string RepositoryRoot = "-ucliRepositoryRoot";

    /// <summary> Gets the argument name that carries project fingerprint value. </summary>
    public const string ProjectFingerprint = "-ucliProjectFingerprint";

    /// <summary> Gets the argument name that carries daemon session path. </summary>
    public const string SessionPath = "-ucliSessionPath";

    /// <summary> Gets the argument name that carries endpoint transport kind literal. </summary>
    public const string EndpointTransportKind = "-ucliEndpointTransportKind";

    /// <summary> Gets the argument name that carries endpoint address. </summary>
    public const string EndpointAddress = "-ucliEndpointAddress";
}