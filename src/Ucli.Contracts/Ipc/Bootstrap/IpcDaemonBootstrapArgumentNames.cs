namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines command-line argument names used to bootstrap Unity IPC daemon mode. </summary>
public static class IpcDaemonBootstrapArgumentNames
{
    /// <summary> Gets the argument name that carries repository root path. </summary>
    public const string RepositoryRoot = "-ucliRepositoryRoot";

    /// <summary> Gets the argument name that carries daemon session path. </summary>
    public const string SessionPath = "-ucliSessionPath";

    /// <summary> Gets the argument name that carries the daemon session generation identifier. </summary>
    public const string SessionGenerationId = "-ucliSessionGenerationId";

    /// <summary> Gets the argument name that carries daemon session issuance timestamp. </summary>
    public const string SessionIssuedAtUtc = "-ucliSessionIssuedAtUtc";
}
