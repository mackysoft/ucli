using ConsoleAppFramework;
using MackySoft.Ucli.Cli;
using MackySoft.Ucli.Composition;
using MackySoft.Ucli.Execution;

namespace MackySoft.Ucli;

internal static class Program
{
    private const string InternalErrorMessage = "An unexpected internal error occurred.";

    private const string CanceledMessage = "Command execution was canceled.";

    /// <summary> Executes the CLI command pipeline and emits JSON command results. </summary>
    /// <param name="args"> The command-line arguments passed to the process. </param>
    /// <returns> The process exit code determined by command execution. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="args" /> is <see langword="null" />. </exception>
    private static async Task<int> Main (string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        ParseErrorBuffer.Clear();
        CommandExecutionState.Reset();

        ConsoleApp.LogError = message =>
        {
            ParseErrorBuffer.Add(message);
            Console.Error.WriteLine(message);
        };

        if (TryHandleUnknownCommand(args))
        {
            return Environment.ExitCode;
        }

        var app = ConsoleApp.Create()
            .ConfigureServices(services =>
            {
                services.AddUcliCoreServices();
                services.AddUcliDaemonServices();
                services.AddUcliTestRunServices();
                services.AddUcliStatusServices();
            });
        app.UseFilter<OperationCatalogWarmupFilter>();
        app.Add<InitCommand>();
        app.Add<StatusCommand>();
        app.Add<DaemonStartCommand>("daemon");
        app.Add<DaemonStopCommand>("daemon");
        app.Add<DaemonStatusCommand>("daemon");
        app.Add<LogsDaemonCommand>("logs");
        app.Add<LogsUnityCommand>("logs");
        app.Add<TestRunCommand>("test");
        app.Add<TestProfileInitCommand>("test profile");

        try
        {
            await app.RunAsync(args);
        }
        catch (OperationCanceledException)
        {
            var canceledResult = CommandResult.Canceled(UcliCommandNames.Root, CanceledMessage);
            CommandResultWriter.WriteToStandardOutput(canceledResult);
            Environment.ExitCode = canceledResult.ExitCode;
            return Environment.ExitCode;
        }
        catch (Exception)
        {
            var internalErrorResult = CommandResult.InternalError(UcliCommandNames.Root, InternalErrorMessage);
            CommandResultWriter.WriteToStandardOutput(internalErrorResult);
            Environment.ExitCode = internalErrorResult.ExitCode;
            return Environment.ExitCode;
        }

        // NOTE:
        // ConsoleAppFramework can fail before command handlers start when parsing options.
        // Emit JSON contract output in that path to keep stdout machine-readable.
        if (!CommandExecutionState.HasStarted && ParseErrorBuffer.HasAny)
        {
            var commandName = UcliCommandNames.ResolveResultCommandName(args);
            var parseErrorMessage = string.Join(" ", ParseErrorBuffer.Messages);
            var parseErrorResult = CommandResult.InvalidArgument(commandName, parseErrorMessage);
            CommandResultWriter.WriteToStandardOutput(parseErrorResult);
            Environment.ExitCode = parseErrorResult.ExitCode;
        }

        return Environment.ExitCode;
    }

    /// <summary> Handles unknown command names before framework dispatch starts. </summary>
    /// <param name="args"> The command-line arguments passed to the process. </param>
    /// <returns>
    /// <para> <see langword="true" /> when this method writes an error response and sets <see cref="Environment.ExitCode" />. </para>
    /// <para> Otherwise, <see langword="false" />. </para>
    /// </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="args" /> is <see langword="null" />. </exception>
    private static bool TryHandleUnknownCommand (string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Length == 0)
        {
            return false;
        }

        var firstArgument = args[0];
        if (CommandTokenClassifier.IsRootCommandToken(firstArgument))
        {
            return false;
        }

        if (string.Equals(firstArgument, UcliCommandNames.Daemon, StringComparison.Ordinal)
            && TryHandleInvalidDaemonSubcommand(args))
        {
            return true;
        }

        if (string.Equals(firstArgument, UcliCommandNames.Logs, StringComparison.Ordinal)
            && TryHandleInvalidLogsSubcommand(args))
        {
            return true;
        }

        if (UcliCommandNames.IsRegistered(firstArgument)
            || string.Equals(firstArgument, UcliCommandNames.Help, StringComparison.Ordinal))
        {
            return false;
        }

        var result = CommandResult.InvalidArgument(
            command: UcliCommandNames.Root,
            message: $"Command '{firstArgument}' is not recognized.");
        CommandResultWriter.WriteToStandardOutput(result);
        Environment.ExitCode = result.ExitCode;
        return true;
    }

    /// <summary> Handles invalid <c>daemon</c> subcommand tokens before framework dispatch starts. </summary>
    /// <param name="args"> The command-line arguments passed to the process. </param>
    /// <returns>
    /// <para> <see langword="true" /> when this method writes an error response and sets <see cref="Environment.ExitCode" />. </para>
    /// <para> Otherwise, <see langword="false" />. </para>
    /// </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="args" /> is <see langword="null" />. </exception>
    private static bool TryHandleInvalidDaemonSubcommand (string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Length == 1)
        {
            var missingSubcommandResult = CommandResult.InvalidArgument(
                command: UcliCommandNames.Daemon,
                message: "Subcommand is required for command 'daemon'. Supported subcommands: start, stop, status.");
            CommandResultWriter.WriteToStandardOutput(missingSubcommandResult);
            Environment.ExitCode = missingSubcommandResult.ExitCode;
            return true;
        }

        var secondArgument = args[1];
        if (CommandTokenClassifier.IsHelpOptionToken(secondArgument)
            || CommandTokenClassifier.IsVersionOptionToken(secondArgument))
        {
            return false;
        }

        if (string.Equals(secondArgument, UcliCommandNames.StartSubcommand, StringComparison.Ordinal)
            || string.Equals(secondArgument, UcliCommandNames.StopSubcommand, StringComparison.Ordinal)
            || string.Equals(secondArgument, UcliCommandNames.Status, StringComparison.Ordinal))
        {
            return false;
        }

        var invalidSubcommandResult = CommandResult.InvalidArgument(
            command: UcliCommandNames.Daemon,
            message: $"Subcommand '{secondArgument}' is not recognized for command 'daemon'.");
        CommandResultWriter.WriteToStandardOutput(invalidSubcommandResult);
        Environment.ExitCode = invalidSubcommandResult.ExitCode;
        return true;
    }

    /// <summary> Handles invalid <c>logs</c> subcommand tokens before framework dispatch starts. </summary>
    /// <param name="args"> The command-line arguments passed to the process. </param>
    /// <returns>
    /// <para> <see langword="true" /> when this method writes an error response and sets <see cref="Environment.ExitCode" />. </para>
    /// <para> Otherwise, <see langword="false" />. </para>
    /// </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="args" /> is <see langword="null" />. </exception>
    private static bool TryHandleInvalidLogsSubcommand (string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Length == 1)
        {
            var missingSubcommandResult = CommandResult.InvalidArgument(
                command: UcliCommandNames.Logs,
                message: "Subcommand is required for command 'logs'. Supported subcommands: daemon, unity.");
            CommandResultWriter.WriteToStandardOutput(missingSubcommandResult);
            Environment.ExitCode = missingSubcommandResult.ExitCode;
            return true;
        }

        var secondArgument = args[1];
        if (CommandTokenClassifier.IsHelpOptionToken(secondArgument)
            || CommandTokenClassifier.IsVersionOptionToken(secondArgument))
        {
            return false;
        }

        if (string.Equals(secondArgument, UcliCommandNames.Daemon, StringComparison.Ordinal)
            || string.Equals(secondArgument, UcliCommandNames.UnitySubcommand, StringComparison.Ordinal))
        {
            return false;
        }

        var invalidSubcommandResult = CommandResult.InvalidArgument(
            command: UcliCommandNames.Logs,
            message: $"Subcommand '{secondArgument}' is not recognized for command 'logs'.");
        CommandResultWriter.WriteToStandardOutput(invalidSubcommandResult);
        Environment.ExitCode = invalidSubcommandResult.ExitCode;
        return true;
    }

}