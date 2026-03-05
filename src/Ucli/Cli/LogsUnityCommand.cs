using ConsoleAppFramework;

namespace MackySoft.Ucli.Cli;

/// <summary> Provides the <c>logs unity</c> CLI command entry point. </summary>
internal sealed class LogsUnityCommand
{
    /// <summary> Executes the <c>logs unity</c> placeholder command and emits not-implemented JSON contract. </summary>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(UcliCommandNames.UnitySubcommand)]
    public Task<int> Unity (CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommandExecutionState.MarkStarted();

        var result = CommandResult.NotImplemented(UcliCommandNames.LogsUnity);
        CommandResultWriter.WriteToStandardOutput(result);
        return Task.FromResult(result.ExitCode);
    }
}