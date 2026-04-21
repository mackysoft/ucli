using MackySoft.Ucli.Hosting.Cli.Common.Parsing;

namespace MackySoft.Ucli.Hosting.Cli.Common.Contracts;

/// <summary> Defines CLI command-name constants and lookup helpers. </summary>
internal static class UcliCommandNames
{
    /// <summary> Gets the command name used when no subcommand can be identified. </summary>
    public const string Root = "root";

    /// <summary> Gets the command name for help. </summary>
    public const string Help = "help";

    /// <summary> Gets the command name for init. </summary>
    public const string Init = "init";

    /// <summary> Gets the command name for status. </summary>
    public const string Status = "status";

    /// <summary> Gets the command name for refresh. </summary>
    public const string Refresh = "refresh";

    /// <summary> Gets the command name for validate. </summary>
    public const string Validate = "validate";

    /// <summary> Gets the command name for plan. </summary>
    public const string Plan = "plan";

    /// <summary> Gets the command name for call. </summary>
    public const string Call = "call";

    /// <summary> Gets the top-level command name for daemon. </summary>
    public const string Daemon = "daemon";

    /// <summary> Gets the command name for <c>daemon start</c> result payloads. </summary>
    public const string DaemonStart = "daemon.start";

    /// <summary> Gets the command name for <c>daemon stop</c> result payloads. </summary>
    public const string DaemonStop = "daemon.stop";

    /// <summary> Gets the command name for <c>daemon cleanup</c> result payloads. </summary>
    public const string DaemonCleanup = "daemon.cleanup";

    /// <summary> Gets the command name for <c>daemon status</c> result payloads. </summary>
    public const string DaemonStatus = "daemon.status";

    /// <summary> Gets the command name for <c>daemon list</c> result payloads. </summary>
    public const string DaemonList = "daemon.list";

    /// <summary> Gets the top-level command name for logs. </summary>
    public const string Logs = "logs";

    /// <summary> Gets the top-level command name for ops. </summary>
    public const string Ops = "ops";

    /// <summary> Gets the command name for <c>logs daemon</c> result payloads. </summary>
    public const string LogsDaemon = "logs.daemon";

    /// <summary> Gets the command name for <c>logs unity</c> result payloads. </summary>
    public const string LogsUnity = "logs.unity";

    /// <summary> Gets the command name for <c>ops list</c> result payloads. </summary>
    public const string OpsList = "ops.list";

    /// <summary> Gets the command name for <c>ops describe</c> result payloads. </summary>
    public const string OpsDescribe = "ops.describe";

    /// <summary> Gets the top-level command name for test. </summary>
    public const string Test = "test";

    /// <summary> Gets the command name for <c>test profile init</c> result payloads. </summary>
    public const string TestProfileInit = "test.profile.init";

    /// <summary> Gets the command name for <c>test run</c> result payloads. </summary>
    public const string TestRun = "test.run";

    /// <summary> Gets the nested command name for profile. </summary>
    public const string Profile = "profile";

    /// <summary> Gets the nested command name for run. </summary>
    public const string RunSubcommand = "run";

    /// <summary> Gets the nested command name for init. </summary>
    public const string InitSubcommand = "init";

    /// <summary> Gets the nested command name for <c>ops list</c>. </summary>
    public const string ListSubcommand = "list";

    /// <summary> Gets the nested command name for <c>ops describe</c>. </summary>
    public const string DescribeSubcommand = "describe";

    /// <summary> Gets the nested command name for daemon start. </summary>
    public const string StartSubcommand = "start";

    /// <summary> Gets the nested command name for daemon stop. </summary>
    public const string StopSubcommand = "stop";

    /// <summary> Gets the nested command name for daemon cleanup. </summary>
    public const string CleanupSubcommand = "cleanup";

    /// <summary> Gets the nested command name for logs unity target. </summary>
    public const string UnitySubcommand = "unity";

    private static readonly HashSet<string> RegisteredCommandNames = new(StringComparer.Ordinal)
    {
        Init,
        Status,
        Refresh,
        Validate,
        Plan,
        Call,
        Daemon,
        Logs,
        Ops,
        Test,
    };

    /// <summary> Determines whether the specified command name is registered in the CLI host. </summary>
    /// <param name="commandName"> The command name to check. </param>
    /// <returns> <see langword="true" /> when the command is registered; otherwise <see langword="false" />. </returns>
    public static bool IsRegistered (string? commandName)
    {
        return !string.IsNullOrWhiteSpace(commandName) && RegisteredCommandNames.Contains(commandName);
    }

    /// <summary> Resolves the command name emitted in parse-error responses. </summary>
    /// <param name="args"> The command-line arguments passed to the process. </param>
    /// <returns>
    /// <para> A command name compatible with the CLI result envelope. </para>
    /// <para> Returns <see cref="Root" /> when no known command can be identified. </para>
    /// </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="args" /> is <see langword="null" />. </exception>
    public static string ResolveResultCommandName (string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Length == 0)
        {
            return Root;
        }

        var firstArgument = args[0];
        if (CommandTokenClassifier.IsRootCommandToken(firstArgument))
        {
            return Root;
        }

        if (string.Equals(firstArgument, Test, StringComparison.Ordinal))
        {
            if (args.Length >= 2
                && string.Equals(args[1], RunSubcommand, StringComparison.Ordinal))
            {
                return TestRun;
            }

            if (args.Length >= 3
                && string.Equals(args[1], Profile, StringComparison.Ordinal)
                && string.Equals(args[2], InitSubcommand, StringComparison.Ordinal))
            {
                return TestProfileInit;
            }

            return Test;
        }

        if (string.Equals(firstArgument, Daemon, StringComparison.Ordinal))
        {
            if (args.Length >= 2
                && string.Equals(args[1], StartSubcommand, StringComparison.Ordinal))
            {
                return DaemonStart;
            }

            if (args.Length >= 2
                && string.Equals(args[1], StopSubcommand, StringComparison.Ordinal))
            {
                return DaemonStop;
            }

            if (args.Length >= 2
                && string.Equals(args[1], CleanupSubcommand, StringComparison.Ordinal))
            {
                return DaemonCleanup;
            }

            if (args.Length >= 2
                && string.Equals(args[1], Status, StringComparison.Ordinal))
            {
                return DaemonStatus;
            }

            if (args.Length >= 2
                && string.Equals(args[1], ListSubcommand, StringComparison.Ordinal))
            {
                return DaemonList;
            }

            return Daemon;
        }

        if (string.Equals(firstArgument, Logs, StringComparison.Ordinal))
        {
            if (args.Length >= 2
                && string.Equals(args[1], Daemon, StringComparison.Ordinal))
            {
                return LogsDaemon;
            }

            if (args.Length >= 2
                && string.Equals(args[1], UnitySubcommand, StringComparison.Ordinal))
            {
                return LogsUnity;
            }

            return Logs;
        }

        if (string.Equals(firstArgument, Ops, StringComparison.Ordinal))
        {
            if (args.Length >= 2
                && string.Equals(args[1], ListSubcommand, StringComparison.Ordinal))
            {
                return OpsList;
            }

            if (args.Length >= 2
                && string.Equals(args[1], DescribeSubcommand, StringComparison.Ordinal))
            {
                return OpsDescribe;
            }

            return Ops;
        }

        return IsRegistered(firstArgument) ? firstArgument : Root;
    }
}