namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines Play Mode wait target literals. </summary>
public static class IpcPlayWaitTargetNames
{
    /// <summary> Gets the wait target for observing active Play Mode. </summary>
    public const string Entered = "entered";

    /// <summary> Gets the wait target for observing Edit Mode after Play Mode has stopped. </summary>
    public const string Exited = "exited";

    /// <summary> Gets the wait target for observing normal execution readiness after Play Mode has stopped. </summary>
    public const string Ready = "ready";
}
