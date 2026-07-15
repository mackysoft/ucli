namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines command-line argument names used to bootstrap Unity oneshot mode. </summary>
public static class IpcOneshotBootstrapArgumentNames
{
    /// <summary> Gets the argument name that carries the non-secret bootstrap-envelope identifier. </summary>
    public const string BootstrapId = "-ucliOneshotBootstrapId";
}
