namespace MackySoft.Ucli.Foundation;

/// <summary> Defines high-level error categories used by foundation services. </summary>
internal enum ExecutionErrorKind
{
    /// <summary> Indicates an invalid argument or invalid contract input. </summary>
    InvalidArgument = 0,

    /// <summary> Indicates an unexpected infrastructure or runtime failure. </summary>
    InternalError = 1,
}