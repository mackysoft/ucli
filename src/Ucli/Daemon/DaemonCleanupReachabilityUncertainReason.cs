namespace MackySoft.Ucli.Daemon;

/// <summary> Represents one known reason why cleanup reachability could not safely prove daemon absence. </summary>
internal enum DaemonCleanupReachabilityUncertainReason
{
    Timeout = 0,
    ConnectTimeout = 1,
    SessionAuthenticationRejected = 2,
    TransportError = 3,
}