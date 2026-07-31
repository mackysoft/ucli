namespace MackySoft.Ucli.Infrastructure.Execution;

/// <summary>
/// Describes whether an operating-system observation proves the identity of one
/// expected process generation.
/// </summary>
internal enum ProcessIdentityObservation
{
    /// <summary> The expected process generation is still running. </summary>
    Same = 1,

    /// <summary>
    /// The expected process exited, or its identifier now belongs to another
    /// process generation.
    /// </summary>
    ConfirmedExitedOrReplaced,

    /// <summary>
    /// The operating system did not provide enough evidence to decide whether
    /// the expected process generation is still running.
    /// </summary>
    Unobservable,
}
