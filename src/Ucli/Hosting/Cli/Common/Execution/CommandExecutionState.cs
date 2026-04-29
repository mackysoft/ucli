namespace MackySoft.Ucli.Hosting.Cli.Common.Execution;

/// <summary> Tracks whether any command handler has started during the current process execution. </summary>
internal static class CommandExecutionState
{
    /// <summary> Gets a value indicating whether command handler execution has started. </summary>
    public static bool HasStarted { get; private set; }

    /// <summary> Marks the process state as started when command handler execution begins. </summary>
    public static void MarkStarted ()
    {
        HasStarted = true;
    }

    /// <summary> Resets the process state before a new command invocation flow. </summary>
    public static void Reset ()
    {
        HasStarted = false;
    }
}
