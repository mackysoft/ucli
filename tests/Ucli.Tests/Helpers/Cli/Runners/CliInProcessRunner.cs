using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Tests;

internal static class CliInProcessRunner
{
    public static async Task<CommandExecutionResult> RunCommandAsync (params string[] args)
    {
        return await RunCommandCoreAsync(
            workingDirectory: null,
            environmentVariables: null,
            standardInput: null,
            args).ConfigureAwait(false);
    }

    public static async Task<CommandExecutionResult> RunCommandWithEnvironmentAsync (
        IReadOnlyDictionary<string, string?> environmentVariables,
        params string[] args)
    {
        ArgumentNullException.ThrowIfNull(environmentVariables);

        return await RunCommandCoreAsync(
            workingDirectory: null,
            environmentVariables,
            standardInput: null,
            args).ConfigureAwait(false);
    }

    public static async Task<CommandExecutionResult> RunCommandWithWorkingDirectoryAsync (
        string workingDirectory,
        params string[] args)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);

        return await RunCommandCoreAsync(
            workingDirectory,
            environmentVariables: null,
            standardInput: null,
            args).ConfigureAwait(false);
    }

    public static async Task<CommandExecutionResult> RunCommandWithStandardInputAsync (
        string standardInput,
        params string[] args)
    {
        ArgumentNullException.ThrowIfNull(standardInput);

        return await RunCommandCoreAsync(
            workingDirectory: null,
            environmentVariables: null,
            standardInput,
            args).ConfigureAwait(false);
    }

    private static async Task<CommandExecutionResult> RunCommandCoreAsync (
        string? workingDirectory,
        IReadOnlyDictionary<string, string?>? environmentVariables,
        string? standardInput,
        string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        await ConsoleAppTestSynchronization.Lock.WaitAsync();
        try
        {
            var previousExitCode = Environment.ExitCode;
            var previousInput = Console.In;
            try
            {
                Environment.ExitCode = (int)CliExitCode.Success;
                using var currentDirectoryScope = workingDirectory is null
                    ? null
                    : new CurrentDirectoryScope(workingDirectory);
                using var environmentVariableScope = environmentVariables is null
                    ? null
                    : new EnvironmentVariableScope(environmentVariables);

                if (standardInput is not null)
                {
                    Console.SetIn(new StringReader(standardInput));
                }

                return await CommandResultCapture.ExecuteWithErrorAsync(async () =>
                    await new CliExecutionRunner().RunAsync(args).ConfigureAwait(false));
            }
            finally
            {
                Console.SetIn(previousInput);
                Environment.ExitCode = previousExitCode;
            }
        }
        finally
        {
            ConsoleAppTestSynchronization.Lock.Release();
        }
    }
}
