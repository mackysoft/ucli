namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Identifies the bounded window option field that failed validation. </summary>
public enum BoundedWindowInvalidField
{
    /// <summary> No bounded window option failed validation. </summary>
    None,

    /// <summary> The unbounded all option conflicts with another window option. </summary>
    All,

    /// <summary> The limit option failed validation. </summary>
    Limit,

    /// <summary> The cursor option failed validation. </summary>
    Cursor,
}
