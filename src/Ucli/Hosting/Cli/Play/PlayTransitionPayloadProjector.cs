using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Hosting.Cli.Play;

/// <summary> Creates public transition payload fragments for Play Mode commands. </summary>
internal static class PlayTransitionPayloadProjector
{
    /// <summary> Creates the JSON-serializable transition payload. </summary>
    /// <param name="transition"> The transition command literal. </param>
    /// <param name="result"> The transition result literal. </param>
    /// <param name="before"> The before snapshot payload. </param>
    /// <param name="after"> The after snapshot payload for successful transitions. </param>
    /// <param name="observed"> The observed snapshot payload for transition errors. </param>
    /// <param name="applicationState"> The application-state literal for transition errors. </param>
    /// <returns> The anonymous payload object serialized by the command-result writer. </returns>
    public static object Create (
        string transition,
        string result,
        object before,
        object? after,
        object? observed,
        string? applicationState)
    {
        return result switch
        {
            IpcPlayTransitionResultNames.Entered
                or IpcPlayTransitionResultNames.AlreadyEntered
                or IpcPlayTransitionResultNames.Exited
                or IpcPlayTransitionResultNames.AlreadyExited => new
                {
                    transition,
                    result,
                    before,
                    after,
                },
            IpcPlayTransitionResultNames.Timeout or IpcPlayTransitionResultNames.Blocked => new
            {
                transition,
                result,
                before,
                observed,
                applicationState,
            },
            _ => new
            {
                transition,
                result,
                before,
            },
        };
    }
}
