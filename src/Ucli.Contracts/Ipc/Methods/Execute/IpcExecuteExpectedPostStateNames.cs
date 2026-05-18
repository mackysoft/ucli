namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines expected post-mutation state literals emitted by execute post-read source facts. </summary>
public static class IpcExecuteExpectedPostStateNames
{
    /// <summary> Indicates that the source describes a deterministic post-state observation target. </summary>
    public const string Deterministic = "deterministic";

    /// <summary> Indicates that the expected post-state cannot be derived from the source alone. </summary>
    public const string Unavailable = "unavailable";
}
