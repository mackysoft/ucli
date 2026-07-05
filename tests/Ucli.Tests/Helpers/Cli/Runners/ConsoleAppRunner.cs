using ConsoleAppFramework;
using MackySoft.Ucli.Hosting.Cli.Common.Startup;

namespace MackySoft.Tests;

internal static class ConsoleAppRunner
{
    public static async Task RunWithRegisteredAppAsync (
        IServiceProvider serviceProvider,
        Func<ConsoleApp.ConsoleAppBuilder, Task> action)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(action);

        await ConsoleAppTestSynchronization.Lock.WaitAsync();
        try
        {
            var app = UcliCommandCatalog.RegisterCommands(ConsoleApp.Create());
            var previousServiceProvider = ConsoleApp.ServiceProvider;

            try
            {
                ConsoleApp.ServiceProvider = serviceProvider;
                await action(app);
            }
            finally
            {
                ConsoleApp.ServiceProvider = previousServiceProvider;
            }
        }
        finally
        {
            ConsoleAppTestSynchronization.Lock.Release();
        }
    }

    public static async Task<CommandExecutionResult> RunAsync (
        ConsoleApp.ConsoleAppBuilder app,
        params string[] args)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(args);

        var previousExitCode = Environment.ExitCode;

        try
        {
            Environment.ExitCode = (int)CliExitCode.Success;
            return await CommandResultCapture.ExecuteWithErrorAsync(async () =>
            {
                await app.RunAsync(args, disposeServiceProvider: false).ConfigureAwait(false);
                return Environment.ExitCode;
            });
        }
        finally
        {
            Environment.ExitCode = previousExitCode;
        }
    }
}
