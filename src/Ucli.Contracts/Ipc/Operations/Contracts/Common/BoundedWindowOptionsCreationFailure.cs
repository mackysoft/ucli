namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Identifies why normalized bounded window options could not be created. </summary>
internal enum BoundedWindowOptionsCreationFailure
{
    /// <summary> No creation failure occurred. </summary>
    None = 0,

    /// <summary> Unbounded mode was combined with bounded window inputs. </summary>
    AllConflict = 1,

    /// <summary> The requested limit was outside the supported range. </summary>
    LimitOutOfRange = 2,

    /// <summary> The supplied cursor was not canonical. </summary>
    InvalidCursor = 3,
}
