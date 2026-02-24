using ConsoleAppFramework;

namespace MackySoft.Ucli.Cli
{
    /// <summary> Provides the <c>init</c> CLI command entry point. </summary>
    internal sealed class InitCommand
    {
        /// <summary> Gets the command name used by this command handler. </summary>
        internal const string CommandName = "init";

        /// <summary> Writes the placeholder result for the <c>init</c> command. </summary>
        /// <param name="force"> Reserved option for future overwrite behavior. The current implementation ignores this value. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by the command pipeline. </param>
        /// <returns> The exit code contained in the emitted command result. </returns>
        [Command(CommandName)]
        public int Init (
            bool force = false,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CommandExecutionState.MarkStarted();

            _ = force;

            var result = CommandResult.NotImplemented(CommandName);
            CommandResultWriter.WriteToStandardOutput(result);
            return result.ExitCode;
        }
    }
}