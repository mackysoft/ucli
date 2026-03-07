using MackySoft.Ucli.Ops;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.Ucli.Cli;

/// <summary> Dispatches <c>ops</c> commands outside ConsoleAppFramework to preserve shared option names across subcommands. </summary>
internal static class OpsCliDispatcher
{
    /// <summary> Tries to dispatch one <c>ops</c> command invocation. </summary>
    /// <param name="args"> The command-line arguments passed to the process. </param>
    /// <param name="configureServices"> The shared service-registration callback. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns>
    /// <para> <see langword="true" /> when the <c>ops</c> command was handled and <see cref="Environment.ExitCode" /> was set. </para>
    /// <para> Otherwise, <see langword="false" />. </para>
    /// </returns>
    public static async Task<bool> TryDispatch (
        string[] args,
        Action<IServiceCollection> configureServices,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(configureServices);
        cancellationToken.ThrowIfCancellationRequested();

        if (args.Length == 0
            || !string.Equals(args[0], UcliCommandNames.Ops, StringComparison.Ordinal))
        {
            return false;
        }

        if (ShouldPrintHelp(args))
        {
            PrintHelp(args);
            Environment.ExitCode = (int)CliExitCode.Success;
            return true;
        }

        if (args.Length < 2)
        {
            return false;
        }

        return args[1] switch
        {
            UcliCommandNames.ListSubcommand => await DispatchList(args, configureServices, cancellationToken).ConfigureAwait(false),
            UcliCommandNames.DescribeSubcommand => await DispatchDescribe(args, configureServices, cancellationToken).ConfigureAwait(false),
            _ => false,
        };
    }

    private static async Task<bool> DispatchList (
        string[] args,
        Action<IServiceCollection> configureServices,
        CancellationToken cancellationToken)
    {
        if (!TryParseOptions(args, startIndex: 2, out var parsedOptions, out var parseError))
        {
            WriteError(CommandResult.InvalidArgument(UcliCommandNames.OpsList, parseError!));
            return true;
        }

        var services = new ServiceCollection();
        configureServices(services);
        using var serviceProvider = services.BuildServiceProvider();
        var opsService = serviceProvider.GetRequiredService<IOpsService>();

        CommandExecutionState.MarkStarted();

        var serviceResult = await opsService.List(
                new OpsCommandInput(
                    ProjectPath: parsedOptions.ProjectPath,
                    Mode: parsedOptions.Mode,
                    Timeout: parsedOptions.Timeout,
                    ReadIndexMode: parsedOptions.ReadIndexMode),
                cancellationToken)
            .ConfigureAwait(false);
        var commandResult = OpsCommandResultFactory.CreateList(serviceResult);
        WriteError(commandResult);
        return true;
    }

    private static async Task<bool> DispatchDescribe (
        string[] args,
        Action<IServiceCollection> configureServices,
        CancellationToken cancellationToken)
    {
        if (args.Length < 3 || IsOptionToken(args[2]))
        {
            WriteError(CommandResult.InvalidArgument(
                UcliCommandNames.OpsDescribe,
                "Argument '<opName>' is required for command 'ops describe'."));
            return true;
        }

        if (!TryParseOptions(args, startIndex: 3, out var parsedOptions, out var parseError))
        {
            WriteError(CommandResult.InvalidArgument(UcliCommandNames.OpsDescribe, parseError!));
            return true;
        }

        var services = new ServiceCollection();
        configureServices(services);
        using var serviceProvider = services.BuildServiceProvider();
        var opsService = serviceProvider.GetRequiredService<IOpsService>();

        CommandExecutionState.MarkStarted();

        var serviceResult = await opsService.Describe(
                new OpsDescribeCommandInput(
                    OperationName: args[2],
                    ProjectPath: parsedOptions.ProjectPath,
                    Mode: parsedOptions.Mode,
                    Timeout: parsedOptions.Timeout,
                    ReadIndexMode: parsedOptions.ReadIndexMode),
                cancellationToken)
            .ConfigureAwait(false);
        var commandResult = OpsCommandResultFactory.CreateDescribe(serviceResult);
        WriteError(commandResult);
        return true;
    }

    private static bool TryParseOptions (
        string[] args,
        int startIndex,
        out ParsedOpsOptions options,
        out string? error)
    {
        options = new ParsedOpsOptions();

        for (var i = startIndex; i < args.Length; i++)
        {
            var argument = args[i];
            if (!IsOptionToken(argument))
            {
                error = $"Argument '{argument}' is not recognized.";
                return false;
            }

            switch (argument)
            {
                case "--projectPath":
                    if (!TryReadOptionValue(args, ref i, argument, out var projectPath, out error))
                    {
                        return false;
                    }

                    options.ProjectPath = projectPath;
                    break;

                case "--mode":
                    if (!TryReadOptionValue(args, ref i, argument, out var mode, out error))
                    {
                        return false;
                    }

                    options.Mode = mode;
                    break;

                case "--timeout":
                    if (!TryReadOptionValue(args, ref i, argument, out var timeout, out error))
                    {
                        return false;
                    }

                    options.Timeout = timeout;
                    break;

                case "--readIndexMode":
                    if (!TryReadOptionValue(args, ref i, argument, out var readIndexMode, out error))
                    {
                        return false;
                    }

                    options.ReadIndexMode = readIndexMode;
                    break;

                default:
                    error = $"Argument '{argument}' is not recognized.";
                    return false;
            }
        }

        error = null;
        return true;
    }

    private static bool TryReadOptionValue (
        string[] args,
        ref int index,
        string optionName,
        out string value,
        out string? error)
    {
        var nextIndex = index + 1;
        if (nextIndex >= args.Length || IsOptionToken(args[nextIndex]))
        {
            value = string.Empty;
            error = $"Argument '{optionName}' expects a value.";
            return false;
        }

        value = args[nextIndex];
        error = null;
        index = nextIndex;
        return true;
    }

    private static bool ShouldPrintHelp (string[] args)
    {
        for (var i = 1; i < args.Length; i++)
        {
            if (CommandTokenClassifier.IsHelpOptionToken(args[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static void PrintHelp (string[] args)
    {
        if (args.Length >= 2
            && string.Equals(args[1], UcliCommandNames.DescribeSubcommand, StringComparison.Ordinal))
        {
            Console.WriteLine("ucli ops describe <opName> [--projectPath <path>] [--mode <auto|daemon|oneshot>] [--timeout <int>] [--readIndexMode <disabled|allowStale|requireFresh>]");
            return;
        }

        Console.WriteLine("ucli ops list [--projectPath <path>] [--mode <auto|daemon|oneshot>] [--timeout <int>] [--readIndexMode <disabled|allowStale|requireFresh>]");
        Console.WriteLine("ucli ops describe <opName> [--projectPath <path>] [--mode <auto|daemon|oneshot>] [--timeout <int>] [--readIndexMode <disabled|allowStale|requireFresh>]");
    }

    private static bool IsOptionToken (string? token)
    {
        return !string.IsNullOrWhiteSpace(token) && token.StartsWith("-", StringComparison.Ordinal);
    }

    private static void WriteError (CommandResult result)
    {
        CommandResultWriter.WriteToStandardOutput(result);
        Environment.ExitCode = result.ExitCode;
    }

    private sealed class ParsedOpsOptions
    {
        public string? ProjectPath { get; set; }

        public string? Mode { get; set; }

        public string? Timeout { get; set; }

        public string? ReadIndexMode { get; set; }
    }
}