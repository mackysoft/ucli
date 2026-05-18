namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines stable ping client-version values used for host lifecycle contracts. </summary>
public static class IpcPingClientVersions
{
    /// <summary> Identifies the startup probe used before dispatching a oneshot request. </summary>
    public const string OneshotStartup = "ucli-oneshot-startup";

    /// <summary> Identifies the ready command's oneshot lifecycle probe request. </summary>
    public const string Ready = "ucli-ready";
}
