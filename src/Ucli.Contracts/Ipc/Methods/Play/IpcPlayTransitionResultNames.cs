namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines Play Mode lifecycle command result literals. </summary>
public static class IpcPlayTransitionResultNames
{
    /// <summary> Gets the result used when Play Mode was entered by this request. </summary>
    public const string Entered = "entered";

    /// <summary> Gets the result used when Play Mode was already active. </summary>
    public const string AlreadyEntered = "alreadyEntered";

    /// <summary> Gets the result used when Play Mode was exited by this request. </summary>
    public const string Exited = "exited";

    /// <summary> Gets the result used when Play Mode was already stopped. </summary>
    public const string AlreadyExited = "alreadyExited";

    /// <summary> Gets the result used when a wait request reached its target. </summary>
    public const string Waited = "waited";

    /// <summary> Gets the result used when the requested Play Mode transition exceeded its timeout. </summary>
    public const string Timeout = "timeout";

    /// <summary> Gets the result used when a non-timeout Editor state blocked the requested Play Mode transition. </summary>
    public const string Blocked = "blocked";
}
