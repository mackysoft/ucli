using ConsoleAppFramework;
using MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;
using MackySoft.Ucli.Features.Daemon.Supervisor.Client;
using MackySoft.Ucli.Features.Daemon.Supervisor.Host;
using MackySoft.Ucli.Features.Daemon.Supervisor.Launch;
using MackySoft.Ucli.Features.Daemon.Supervisor.Transport;
using MackySoft.Ucli.Hosting.Cli;
using MackySoft.Ucli.Hosting.Composition;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.Ucli;

internal static class Program
{
    private const string InternalErrorMessage = "An unexpected internal error occurred.";

    private const string CanceledMessage = "Command execution was canceled.";

    private const string InternalSupervisorServeFlag = "--ucli-internal-supervisor-serve";

    private const string InternalRepositoryRootOption = "--repositoryRoot";

    private static readonly string[] DaemonSubcommands =
    [
        UcliCommandNames.StartSubcommand,
        UcliCommandNames.StopSubcommand,
        UcliCommandNames.CleanupSubcommand,
        UcliCommandNames.Status,
        UcliCommandNames.ListSubcommand,
    ];

    private static readonly string[] LogsSubcommands =
    [
        UcliCommandNames.Daemon,
        UcliCommandNames.UnitySubcommand,
    ];

    private static readonly string[] OpsSubcommands =
    [
        UcliCommandNames.ListSubcommand,
        UcliCommandNames.DescribeSubcommand,
    ];

    /// <summary> Executes the CLI command pipeline and emits JSON command results. </summary>
    /// <param name="args"> The command-line arguments passed to the process. </param>
    /// <returns> The process exit code determined by command execution. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="args" /> is <see langword="null" />. </exception>
    private static async Task<int> Main (string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (TryHandleInternalSupervisorServe(args, out var repositoryRoot))
        {
            return await RunSupervisorAsync(repositoryRoot).ConfigureAwait(false);
        }

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
            .ConfigureServices(static services => services.AddUcliServices())
            .RegisterUcliCommands();

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

    private static bool TryHandleInternalSupervisorServe (
        string[] args,
        out string repositoryRoot)
    {
        ArgumentNullException.ThrowIfNull(args);

        repositoryRoot = string.Empty;
        if (args.Length == 0
            || !string.Equals(args[0], InternalSupervisorServeFlag, StringComparison.Ordinal))
        {
            return false;
        }

        if (args.Length != 3
            || !string.Equals(args[1], InternalRepositoryRootOption, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(args[2]))
        {
            return true;
        }

        repositoryRoot = args[2];
        return true;
    }

    private static async Task<int> RunSupervisorAsync (string repositoryRoot)
    {
        if (string.IsNullOrWhiteSpace(repositoryRoot))
        {
            return 1;
        }

        var services = new ServiceCollection();
        services.AddUcliServices();

        await using var serviceProvider = services.BuildServiceProvider();
        var supervisorHost = serviceProvider.GetRequiredService<SupervisorHost>();
        return await supervisorHost.Run(repositoryRoot, CancellationToken.None).ConfigureAwait(false);
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
            && TryHandleInvalidSubcommand(args, UcliCommandNames.Daemon, DaemonSubcommands))
        {
            return true;
        }

        if (string.Equals(firstArgument, UcliCommandNames.Logs, StringComparison.Ordinal)
            && TryHandleInvalidSubcommand(args, UcliCommandNames.Logs, LogsSubcommands))
        {
            return true;
        }

        if (string.Equals(firstArgument, UcliCommandNames.Ops, StringComparison.Ordinal)
            && TryHandleInvalidSubcommand(args, UcliCommandNames.Ops, OpsSubcommands))
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

    /// <summary> Handles invalid top-level subcommand tokens before framework dispatch starts. </summary>
    /// <param name="args"> The command-line arguments passed to the process. </param>
    /// <param name="commandName"> The top-level command name. </param>
    /// <param name="supportedSubcommands"> The supported subcommand token list. </param>
    /// <returns>
    /// <para> <see langword="true" /> when this method writes an error response and sets <see cref="Environment.ExitCode" />. </para>
    /// <para> Otherwise, <see langword="false" />. </para>
    /// </returns>
    /// <exception cref="ArgumentNullException"> Thrown when one argument is <see langword="null" />. </exception>
    private static bool TryHandleInvalidSubcommand (
        string[] args,
        string commandName,
        IReadOnlyList<string> supportedSubcommands)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);
        ArgumentNullException.ThrowIfNull(supportedSubcommands);

        var result = SubcommandValidationHelper.TryCreateInvalidSubcommandResult(args, commandName, supportedSubcommands);
        if (result == null)
        {
            return false;
        }

        CommandResultWriter.WriteToStandardOutput(result);
        Environment.ExitCode = result.ExitCode;
        return true;
    }
}