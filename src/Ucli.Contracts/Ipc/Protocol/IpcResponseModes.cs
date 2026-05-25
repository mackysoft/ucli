namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines IPC response framing modes. </summary>
public static class IpcResponseModes
{
    /// <summary> Gets the single terminal response mode. </summary>
    public const string Single = "single";

    /// <summary> Gets the progress-frame stream followed by one terminal response mode. </summary>
    public const string Stream = "stream";

    /// <summary> Returns whether the supplied response mode is supported by the IPC protocol. </summary>
    /// <param name="responseMode"> The response mode value. </param>
    /// <returns> <see langword="true" /> when the response mode is supported; otherwise <see langword="false" />. </returns>
    public static bool IsDefined (string? responseMode)
    {
        return string.Equals(responseMode, Single, StringComparison.Ordinal)
            || string.Equals(responseMode, Stream, StringComparison.Ordinal);
    }
}
