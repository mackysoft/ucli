namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines command-line argument names used to bootstrap Unity oneshot mode. </summary>
public static class IpcOneshotBootstrapArgumentNames
{
    /// <summary> Gets the argument name that carries one originating CLI parent process identifier. </summary>
    public const string ParentProcessId = "-ucliOneshotParentProcessId";

    /// <summary> Gets the argument name that carries one oneshot-only session token. </summary>
    public const string SessionToken = "-ucliOneshotSessionToken";
}
