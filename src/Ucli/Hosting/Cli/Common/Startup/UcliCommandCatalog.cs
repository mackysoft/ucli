using ConsoleAppFramework;
using MackySoft.AgentSkills.ConsoleAppFramework;
using MackySoft.Ucli.Hosting.Cli.Assurance;
using MackySoft.Ucli.Hosting.Cli.Codes;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Startup.OutputContracts;
using MackySoft.Ucli.Hosting.Cli.Common.Tokens;
using MackySoft.Ucli.Hosting.Cli.Daemon;
using MackySoft.Ucli.Hosting.Cli.Daemon.Logs;
using MackySoft.Ucli.Hosting.Cli.Init;
using MackySoft.Ucli.Hosting.Cli.Ops;
using MackySoft.Ucli.Hosting.Cli.Play;
using MackySoft.Ucli.Hosting.Cli.Programs;
using MackySoft.Ucli.Hosting.Cli.Recording;
using MackySoft.Ucli.Hosting.Cli.Requests;
using MackySoft.Ucli.Hosting.Cli.Schemas;
using MackySoft.Ucli.Hosting.Cli.Screenshot;
using MackySoft.Ucli.Hosting.Cli.Status;
using MackySoft.Ucli.Hosting.Cli.Testing;

namespace MackySoft.Ucli.Hosting.Cli.Common.Startup;

/// <summary> Provides the single catalog for public CLI registration and pre-dispatch metadata. </summary>
internal static class UcliCommandCatalog
{
    private static readonly StandaloneCommandEntry[] StandaloneCommands =
        UcliCommandCatalogDefinition.StandaloneCommands;

    private static readonly CommandGroupEntry[] CommandGroups = UcliCommandCatalogDefinition.CommandGroups;

    private static readonly HashSet<string> RegisteredRootCommandSet = new(
        CreateRegisteredRootCommands(),
        StringComparer.Ordinal);

    private static readonly UnexpectedLeafArgumentRule[] UnexpectedLeafArgumentRules =
        UcliCommandCatalogDefinition.UnexpectedLeafArgumentRules;

    /// <summary> Gets all registered command paths expected in framework help output. </summary>
    public static IReadOnlyList<string> CommandPaths { get; } = CreateCommandPaths();

    /// <summary> Gets the effective serializer contracts emitted by the public command tree. </summary>
    public static IReadOnlyList<UcliCommandOutputContract> OutputContracts { get; } =
        UcliCommandOutputContractCatalog.GetAll();

    /// <summary> Registers all supported uCLI commands with the application builder. </summary>
    /// <param name="app"> The application builder used to register commands. </param>
    /// <returns> The same <paramref name="app" /> instance for call chaining. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="app" /> is <see langword="null" />. </exception>
    public static ConsoleApp.ConsoleAppBuilder RegisterCommands (ConsoleApp.ConsoleAppBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.Add<InitCommand>();
        app.Add<StatusCommand>();
        app.Add<ReadyCommand>();
        app.Add<CompileCommand>();
        app.Add<VerifyCommand>();
        app.Add<BuildRunCommand>("build");
        app.Add<RefreshCommand>();
        app.Add<ResolveCommand>();
        app.Add<QueryAssetsFindCommand>("query assets");
        app.Add<QuerySceneTreeCommand>("query scene");
        app.Add<QueryGoDescribeCommand>("query go");
        app.Add<QueryCompSchemaCommand>("query comp");
        app.Add<QueryAssetSchemaCommand>("query asset");
        app.Add<ValidateCommand>();
        app.Add<PlanCommand>();
        app.Add<CallCommand>();
        app.Add<EvalCommand>();
        app.Add<ProgramValidationCommand>("program");
        app.Add<ProgramPlanCommand>("program");
        app.Add<ProgramRunCommand>("program");
        app.Add<ProgramStatusCommand>("program");
        app.Add<ProgramCancelCommand>("program");
        app.Add<ProgramPresetsListCommand>("program presets");
        app.Add<ProgramPresetsDescribeCommand>("program presets");
        app.Add<DaemonStartCommand>("daemon");
        app.Add<DaemonStopCommand>("daemon");
        app.Add<DaemonCleanupCommand>("daemon");
        app.Add<DaemonStatusCommand>("daemon");
        app.Add<DaemonListCommand>("daemon");
        app.Add<LogsDaemonReadCommand>("logs daemon");
        app.Add<LogsUnityReadCommand>("logs unity");
        app.Add<LogsUnityClearCommand>("logs unity");
        app.Add<ScreenshotGameCommand>("screenshot");
        app.Add<ScreenshotSceneCommand>("screenshot");
        app.Add<GameViewRecordingStartCommand>("recording");
        app.Add<GameViewRecordingStatusCommand>("recording");
        app.Add<GameViewRecordingStopCommand>("recording");
        app.Add<OpsListCommand>("ops");
        app.Add<OpsDescribeCommand>("ops");
        app.Add<SchemaListCommand>("schema");
        app.Add<SchemaGetCommand>("schema");
        app.Add<SchemaExportCommand>("schema");
        app.Add<CodesListCommand>("codes");
        app.Add<CodesDescribeCommand>("codes");
        app.Add<PlayStatusCommand>("play");
        app.Add<PlayEnterCommand>("play");
        app.Add<PlayExitCommand>("play");
        app.RegisterAgentSkillsCommands();
        app.Add<TestRunCommand>("test");
        app.Add<TestProfileInitCommand>("test profile");
        return app;
    }

    /// <summary> Determines whether the specified command name is registered in the CLI host. </summary>
    /// <param name="commandName"> The command name to check. </param>
    /// <returns> <see langword="true" /> when the command is registered; otherwise <see langword="false" />. </returns>
    public static bool IsRegisteredRootCommand (string? commandName)
    {
        return !string.IsNullOrWhiteSpace(commandName) && RegisteredRootCommandSet.Contains(commandName);
    }

    /// <summary> Resolves the command name emitted in parse-error responses. </summary>
    /// <param name="args"> The command-line arguments passed to the process. </param>
    /// <returns>
    /// <para> A command name compatible with the CLI result envelope. </para>
    /// <para> Returns <see cref="UcliCommandNames.Root" /> when no known command can be identified. </para>
    /// </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="args" /> is <see langword="null" />. </exception>
    public static string ResolveResultCommandName (string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Length == 0)
        {
            return UcliCommandNames.Root;
        }

        var firstArgument = args[0];
        if (CommandTokenClassifier.IsRootCommandToken(firstArgument))
        {
            return UcliCommandNames.Root;
        }

        var group = FindCommandGroup(firstArgument);
        if (group != null)
        {
            return ResolveGroupResultCommandName(args, group);
        }

        return IsRegisteredRootCommand(firstArgument) ? firstArgument : UcliCommandNames.Root;
    }

    /// <summary> Creates the structurally valid empty error branch for one result command. </summary>
    public static object CreateDefaultErrorPayload (string commandName)
    {
        return UcliCommandOutputContractCatalog
            .Get(commandName)
            .CreateDefaultErrorPayload();
    }

    /// <summary> Gets supported second-level subcommands for a command group that must be validated before dispatch. </summary>
    /// <param name="commandName"> The root command name. </param>
    /// <param name="supportedSubcommands"> The supported subcommands when found. </param>
    /// <returns> <see langword="true" /> when the root command must be validated before framework dispatch. </returns>
    public static bool TryGetPreDispatchSupportedSubcommands (
        string commandName,
        out IReadOnlyList<string> supportedSubcommands)
    {
        var group = FindCommandGroup(commandName);
        if (group == null)
        {
            supportedSubcommands = Array.Empty<string>();
            return false;
        }

        supportedSubcommands = group.SupportedSubcommands;
        return true;
    }

    /// <summary> Resolves a help invocation that targets a non-executable command group. </summary>
    /// <param name="args"> The command-line arguments passed to the process. </param>
    /// <param name="groupPath"> The resolved command-group path when found. </param>
    /// <returns> <see langword="true" /> when the arguments request help for a known command group. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="args" /> is <see langword="null" />. </exception>
    public static bool TryResolveGroupHelpPath (
        string[] args,
        out string groupPath)
    {
        ArgumentNullException.ThrowIfNull(args);

        groupPath = string.Empty;
        if (args.Length < 2 || !CommandTokenClassifier.IsHelpOptionToken(args[^1]))
        {
            return false;
        }

        if (args.Length == 2 && FindCommandGroup(args[0]) is not null)
        {
            groupPath = args[0];
            return true;
        }

        if (args.Length == 3 && FindNestedCommandGroup(args[0], args[1]) is not null)
        {
            groupPath = $"{args[0]} {args[1]}";
            return true;
        }

        return false;
    }

    /// <summary> Gets supported leaf subcommands for a nested command group. </summary>
    /// <param name="commandName"> The root command name. </param>
    /// <param name="groupName"> The nested command group name. </param>
    /// <param name="supportedSubcommands"> The supported leaf subcommands when found. </param>
    /// <returns> <see langword="true" /> when the nested group exists. </returns>
    public static bool TryGetSupportedLeafSubcommands (
        string commandName,
        string groupName,
        out IReadOnlyList<string> supportedSubcommands)
    {
        var nestedGroup = FindNestedCommandGroup(commandName, groupName);
        if (nestedGroup == null)
        {
            supportedSubcommands = Array.Empty<string>();
            return false;
        }

        supportedSubcommands = nestedGroup.SupportedSubcommands;
        return true;
    }

    /// <summary> Gets a leaf argument validation rule for commands with framework gaps. </summary>
    /// <param name="commandName"> The root command name. </param>
    /// <param name="subcommandName"> The leaf subcommand name. </param>
    /// <param name="rule"> The matched rule when found. </param>
    /// <returns> <see langword="true" /> when a rule applies to the command path. </returns>
    public static bool TryGetUnexpectedLeafArgumentRule (
        string commandName,
        string subcommandName,
        out UnexpectedLeafArgumentRule rule)
    {
        for (var i = 0; i < UnexpectedLeafArgumentRules.Length; i++)
        {
            var candidate = UnexpectedLeafArgumentRules[i];
            if (string.Equals(candidate.CommandName, commandName, StringComparison.Ordinal)
                && string.Equals(candidate.SubcommandName, subcommandName, StringComparison.Ordinal))
            {
                rule = candidate;
                return true;
            }
        }

        rule = default;
        return false;
    }

    private static string ResolveGroupResultCommandName (
        string[] args,
        CommandGroupEntry group)
    {
        if (args.Length < 2)
        {
            return group.CommandName;
        }

        var leaf = group.FindLeaf(args[1]);
        if (leaf != null)
        {
            return leaf.ResultCommandName;
        }

        var nestedGroup = group.FindNestedGroup(args[1]);
        if (nestedGroup == null || args.Length < 3)
        {
            return group.CommandName;
        }

        var nestedLeaf = nestedGroup.FindLeaf(args[2]);
        return nestedLeaf?.ResultCommandName ?? group.CommandName;
    }

    private static CommandGroupEntry? FindCommandGroup (string commandName)
    {
        for (var i = 0; i < CommandGroups.Length; i++)
        {
            var group = CommandGroups[i];
            if (string.Equals(group.CommandName, commandName, StringComparison.Ordinal))
            {
                return group;
            }
        }

        return null;
    }

    private static NestedCommandGroupEntry? FindNestedCommandGroup (
        string commandName,
        string groupName)
    {
        var group = FindCommandGroup(commandName);
        return group?.FindNestedGroup(groupName);
    }

    private static string[] CreateRegisteredRootCommands ()
    {
        var commands = new string[StandaloneCommands.Length + CommandGroups.Length];
        var index = 0;

        for (var i = 0; i < StandaloneCommands.Length; i++)
        {
            commands[index] = StandaloneCommands[i].CommandName;
            index++;
        }

        for (var i = 0; i < CommandGroups.Length; i++)
        {
            commands[index] = CommandGroups[i].CommandName;
            index++;
        }

        return commands;
    }

    private static string[] CreateSupportedSubcommands (
        CommandLeafEntry[] leaves,
        NestedCommandGroupEntry[] nestedGroups)
    {
        var supportedSubcommands = new string[leaves.Length + nestedGroups.Length];
        var index = 0;

        for (var i = 0; i < leaves.Length; i++)
        {
            supportedSubcommands[index] = leaves[i].SubcommandName;
            index++;
        }

        for (var i = 0; i < nestedGroups.Length; i++)
        {
            supportedSubcommands[index] = nestedGroups[i].GroupName;
            index++;
        }

        return supportedSubcommands;
    }

    private static int CountNestedCommandPaths (NestedCommandGroupEntry[] nestedGroups)
    {
        var count = 0;
        for (var i = 0; i < nestedGroups.Length; i++)
        {
            count += nestedGroups[i].Leaves.Length;
        }

        return count;
    }

    private static string[] CreateCommandPaths ()
    {
        var commandPathCount = StandaloneCommands.Length;
        for (var i = 0; i < CommandGroups.Length; i++)
        {
            commandPathCount += CommandGroups[i].CommandPathCount;
        }

        var commandPaths = new string[commandPathCount];
        var index = 0;

        for (var i = 0; i < StandaloneCommands.Length; i++)
        {
            commandPaths[index] = StandaloneCommands[i].CommandName;
            index++;
        }

        for (var i = 0; i < CommandGroups.Length; i++)
        {
            var group = CommandGroups[i];
            group.AddCommandPaths(commandPaths, ref index);
        }

        return commandPaths;
    }

    internal sealed record CommandGroupEntry (
        string CommandName,
        CommandLeafEntry[] Leaves,
        NestedCommandGroupEntry[] NestedGroups)
    {
        public int CommandPathCount { get; } = Leaves.Length + CountNestedCommandPaths(NestedGroups);

        public IReadOnlyList<string> SupportedSubcommands { get; } = CreateSupportedSubcommands(Leaves, NestedGroups);

        public void AddCommandPaths (
            string[] commandPaths,
            ref int index)
        {
            for (var i = 0; i < Leaves.Length; i++)
            {
                var leaf = Leaves[i];
                commandPaths[index] = $"{CommandName} {leaf.SubcommandName}";
                index++;
            }

            for (var i = 0; i < NestedGroups.Length; i++)
            {
                NestedGroups[i].AddCommandPaths(CommandName, commandPaths, ref index);
            }
        }

        public CommandLeafEntry? FindLeaf (string subcommandName)
        {
            for (var i = 0; i < Leaves.Length; i++)
            {
                var leaf = Leaves[i];
                if (string.Equals(leaf.SubcommandName, subcommandName, StringComparison.Ordinal))
                {
                    return leaf;
                }
            }

            return null;
        }

        public NestedCommandGroupEntry? FindNestedGroup (string groupName)
        {
            for (var i = 0; i < NestedGroups.Length; i++)
            {
                var group = NestedGroups[i];
                if (string.Equals(group.GroupName, groupName, StringComparison.Ordinal))
                {
                    return group;
                }
            }

            return null;
        }
    }

    internal sealed record NestedCommandGroupEntry (
        string GroupName,
        CommandLeafEntry[] Leaves)
    {
        public IReadOnlyList<string> SupportedSubcommands { get; } = CreateSupportedSubcommands(Leaves, []);

        public void AddCommandPaths (
            string commandName,
            string[] commandPaths,
            ref int index)
        {
            for (var i = 0; i < Leaves.Length; i++)
            {
                commandPaths[index] = $"{commandName} {GroupName} {Leaves[i].SubcommandName}";
                index++;
            }
        }

        public CommandLeafEntry? FindLeaf (string subcommandName)
        {
            for (var i = 0; i < Leaves.Length; i++)
            {
                var leaf = Leaves[i];
                if (string.Equals(leaf.SubcommandName, subcommandName, StringComparison.Ordinal))
                {
                    return leaf;
                }
            }

            return null;
        }
    }

    internal sealed record StandaloneCommandEntry (
        UcliCommandOutputContract OutputContract)
    {
        public string CommandName => OutputContract.Command;
    }

    internal sealed record CommandLeafEntry (
        string SubcommandName,
        UcliCommandOutputContract OutputContract)
    {
        public string ResultCommandName => OutputContract.Command;
    }

    internal readonly record struct UnexpectedLeafArgumentRule (
        string CommandName,
        string SubcommandName,
        string ResultCommandName,
        int ExpectedArgumentCount);
}
