namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines shared bounded query window limits. </summary>
public static class BoundedWindowConstants
{
    /// <summary> Gets the default bounded query result limit. </summary>
    public const int DefaultLimit = 100;

    /// <summary> Gets the maximum bounded query result limit. </summary>
    public const int MaxLimit = 10000;
}
