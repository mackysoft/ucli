using ConsoleAppFramework;

namespace MackySoft.Ucli.Cli
{
    /// <summary> Provides the <c>status</c> CLI command entry point. </summary>
    internal sealed class StatusCommand
    {
        /// <summary> Gets the command name used by this command handler. </summary>
        internal const string CommandName = "status";

        /// <summary> Writes the placeholder result for the <c>status</c> command. </summary>
        /// <param name="projectPath"> --projectPath, Reserved option for target UnityProject path selection. The current implementation ignores this value. </param>
        /// <param name="mode"> Reserved option for status output mode selection. The current implementation ignores this value. </param>
        /// <returns> The exit code contained in the emitted command result. </returns>
        [Command(CommandName)]
        public int Status (
            string? projectPath = null,
            string? mode = null)
        {
            CommandExecutionState.MarkStarted();

            var result = CommandResult.NotImplemented(CommandName);
            CommandResultWriter.WriteToStandardOutput(result);
            return result.ExitCode;
        }
    }
}