namespace MackySoft.Ucli.Daemon;

/// <summary> Defines categorized failure kinds for daemon session read operation. </summary>
internal enum DaemonSessionReadFailureKind
{
    /// <summary> Indicates session read succeeded without failure. </summary>
    None = 0,

    /// <summary> Indicates failure reason is unknown or not categorized. </summary>
    Unknown = 1,

    /// <summary> Indicates persisted session payload is invalid but recoverable by stale cleanup. </summary>
    InvalidSession = 2,

    /// <summary> Indicates session storage path is invalid. </summary>
    PathInvalid = 3,

    /// <summary> Indicates filesystem I/O failure occurred during read. </summary>
    IoFailure = 4,

    /// <summary> Indicates unexpected internal failure occurred during read. </summary>
    InternalFailure = 5,
}
