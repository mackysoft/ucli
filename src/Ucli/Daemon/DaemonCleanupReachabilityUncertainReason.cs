namespace MackySoft.Ucli.Daemon;

/// <summary> Represents one known reason why cleanup reachability could not safely prove daemon absence. </summary>
internal enum DaemonCleanupReachabilityUncertainReason
{
    /// <summary> The ping request timed out after connection, so cleanup cannot distinguish a hung daemon from daemon absence safely. </summary>
    Timeout = 0,

    /// <summary> The IPC transport could not finish connecting before timeout, which does not prove the canonical endpoint is absent. </summary>
    ConnectTimeout = 1,

    /// <summary> The endpoint responded but rejected cleanup probe authentication, so destructive cleanup must not treat the endpoint as absent. </summary>
    SessionAuthenticationRejected = 2,

    /// <summary> The transport failed without direct endpoint absence evidence, so cleanup must remain non-destructive. </summary>
    TransportError = 3,
}