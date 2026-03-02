using ConsoleAppFramework;
using MackySoft.Ucli.Cli;
using MackySoft.Ucli.Cli.Requests;
using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Context;
using MackySoft.Ucli.Daemon;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Init;
using MackySoft.Ucli.Ipc;
using MackySoft.Ucli.Operations;
using MackySoft.Ucli.TestProfile;
using MackySoft.Ucli.UnityProject;
using MackySoft.Ucli.UnityProject.Resolution;
using Microsoft.Extensions.DependencyInjection;

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
                services.AddSingleton<IUnityProjectResolver, UnityProjectResolver>();
                services.AddSingleton<IUnityVersionResolver, UnityVersionResolver>();
                services.AddSingleton<IUnityEditorSearchRootProvider, DefaultUnityEditorSearchRootProvider>();
                services.AddSingleton<IUnityEditorPathResolver, UnityEditorPathResolver>();
                services.AddSingleton<IUcliConfigStore, UcliConfigStore>();
                services.AddSingleton<IInitStatusContextResolver, InitStatusContextResolver>();
                services.AddSingleton<IInitService, InitService>();
                services.AddSingleton<IIpcEndpointResolver, IpcEndpointResolver>();
                services.AddSingleton<IUnityIpcClient, UnityIpcClient>();
                services.AddSingleton<IDaemonLifecycleLockProvider, FileSystemDaemonLifecycleLockProvider>();
                services.AddSingleton<IDaemonSessionFileAccess, DaemonSessionFileAccess>();
                services.AddSingleton<IDaemonSessionSerializer, DaemonSessionJsonSerializer>();
                services.AddSingleton<IDaemonSessionValidator, DaemonSessionValidator>();
                services.AddSingleton<IDaemonSessionStore, DaemonSessionStore>();
                services.AddSingleton<IDaemonSessionTokenGenerator, DaemonSessionTokenGenerator>();
                services.AddSingleton<IDaemonSessionTokenProvider, DaemonSessionTokenProvider>();
                services.AddSingleton<IDaemonLogReader, DaemonLogReader>();
                services.AddSingleton<IUnityDaemonProcessLauncher, UnityDaemonProcessLauncher>();
                services.AddSingleton<IDaemonPingClient, IpcDaemonPingClient>();
                services.AddSingleton<IDaemonStartupReadinessProbe, DaemonStartupReadinessProbe>();
                services.AddSingleton<IDaemonShutdownClient, DaemonShutdownClient>();
                services.AddSingleton<IDaemonProcessTerminationService, DaemonProcessTerminationService>();
                services.AddSingleton<IDaemonArtifactCleaner, DaemonArtifactCleaner>();
                services.AddSingleton<IDaemonReachabilityClassifier, DaemonReachabilityClassifier>();
                services.AddSingleton<IDaemonStartOperation, DaemonStartOperation>();
                services.AddSingleton<IDaemonStopOperation, DaemonStopOperation>();
                services.AddSingleton<IDaemonStatusOperation, DaemonStatusOperation>();
                services.AddSingleton<IDaemonManagementService, DaemonManagementService>();
                services.AddSingleton<IDaemonReachabilityProbe, IpcDaemonReachabilityProbe>();
                services.AddSingleton<IUnityExecutionModeDecisionService, UnityExecutionModeDecisionService>();
                services.AddSingleton<IOperationCatalogProvider, InMemoryOperationCatalogProvider>();
                services.AddSingleton<IOperationCatalog, OperationCatalog>();
                services.AddSingleton<IOperationAuthorizationService, OperationAuthorizationService>();
                services.AddSingleton<IRequestStaticValidator, RequestStaticValidator>();
                services.AddSingleton<IRequestInputReader, RequestInputReader>();
                services.AddSingleton<IValidateRequestJsonParser, ValidateRequestJsonParser>();
                services.AddSingleton<IPhaseExecutionPreflightService, PhaseExecutionPreflightService>();
                services.AddSingleton<ITestProfileInitService, TestProfileInitService>();
            });
        app.UseFilter<OperationCatalogWarmupFilter>();
        app.Add<InitCommand>();
        app.Add<StatusCommand>();
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

}