using ConsoleAppFramework;

namespace MackySoft.Tests;

internal static class ConsoleAppHelpRunner
{
    public static async Task<CommandExecutionResult> RunHelpAsync (
        IServiceProvider serviceProvider,
        string commandPath)
    {
        CommandExecutionResult result = default;
        await ConsoleAppRunner.RunWithRegisteredAppAsync(
            serviceProvider,
            async app => result = await RunHelpAsync(app, commandPath));
        return result;
    }

    public static async Task<CommandExecutionResult> RunRootHelpAsync (IServiceProvider serviceProvider)
    {
        CommandExecutionResult result = default;
        await ConsoleAppRunner.RunWithRegisteredAppAsync(
            serviceProvider,
            async app => result = await RunRootHelpAsync(app));
        return result;
    }

    public static Task<CommandExecutionResult> RunRootHelpAsync (ConsoleApp.ConsoleAppBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return ConsoleAppRunner.RunAsync(app, "--help");
    }

    public static async Task<CommandExecutionResult> RunHelpAsync (
        ConsoleApp.ConsoleAppBuilder app,
        string commandPath)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentException.ThrowIfNullOrWhiteSpace(commandPath);

        string[] args = commandPath
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Append("--help")
            .ToArray();

        return await ConsoleAppRunner.RunAsync(app, args);
    }
}
