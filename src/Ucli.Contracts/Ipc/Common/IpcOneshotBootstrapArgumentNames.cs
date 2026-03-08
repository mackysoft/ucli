namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines command-line argument names used to bootstrap Unity oneshot mode. </summary>
public static class IpcOneshotBootstrapArgumentNames
{
    /// <summary> Gets the argument name that carries one serialized request file path. </summary>
    public const string RequestPath = "-ucliOneshotRequestPath";

    /// <summary> Gets the argument name that carries one serialized response file path. </summary>
    public const string ResponsePath = "-ucliOneshotResponsePath";
}