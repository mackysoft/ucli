using ConsoleAppFramework;
using MackySoft.Ucli.Cli;
using MackySoft.Ucli.Cli.Requests;
using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Context;
using MackySoft.Ucli.Daemon;
using MackySoft.Ucli.Daemon.Command;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Init;
using MackySoft.Ucli.Ipc;
using MackySoft.Ucli.Operations;
using MackySoft.Ucli.Status;
using MackySoft.Ucli.TestProfile;
using MackySoft.Ucli.TestRun.Artifacts;
using MackySoft.Ucli.TestRun.Configuration;
using MackySoft.Ucli.TestRun.Execution;
using MackySoft.Ucli.TestRun.Results;
using MackySoft.Ucli.TestRun.Service;
using MackySoft.Ucli.TestRun.Service.Mapping;
using MackySoft.Ucli.TestRun.Service.Pipeline;
using MackySoft.Ucli.TestRun.Service.Preflight;
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
                services.AddSingleton<IpcDaemonPingClient>();
                services.AddSingleton<IDaemonPingClient>(provider => provider.GetRequiredService<IpcDaemonPingClient>());
                services.AddSingleton<IDaemonPingInfoClient>(provider => provider.GetRequiredService<IpcDaemonPingClient>());
                services.AddSingleton<IDaemonStartupReadinessProbe, DaemonStartupReadinessProbe>();
                services.AddSingleton<IDaemonShutdownClient, DaemonShutdownClient>();
                services.AddSingleton<IDaemonProcessTerminationService, DaemonProcessTerminationService>();
                services.AddSingleton<IDaemonArtifactCleaner, DaemonArtifactCleaner>();
                services.AddSingleton<IDaemonReachabilityClassifier, DaemonReachabilityClassifier>();
                services.AddSingleton<IDaemonStartOperation, DaemonStartOperation>();
                services.AddSingleton<IDaemonStopOperation, DaemonStopOperation>();
                services.AddSingleton<IDaemonStatusOperation, DaemonStatusOperation>();
                services.AddSingleton<IDaemonCommandExecutionContextResolver, DaemonCommandExecutionContextResolver>();
                services.AddSingleton<IDaemonSessionOutputMapper, DaemonSessionOutputMapper>();
                services.AddSingleton<IDaemonStartCommandService, DaemonStartCommandService>();
                services.AddSingleton<IDaemonStopCommandService, DaemonStopCommandService>();
                services.AddSingleton<IDaemonStatusCommandService, DaemonStatusCommandService>();
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
                services.AddSingleton<IProcessRunner, ProcessRunner>();
                services.AddSingleton<ITestRunMetaStore, TestRunMetaStore>();
                services.AddSingleton<ITestRunArtifactsService, TestRunArtifactsService>();
                services.AddSingleton<IUnityCommandBuilder, UnityCommandBuilder>();
                services.AddSingleton<IUnityTestExecutor, UnityTestExecutor>();
                services.AddSingleton<IDaemonTestRunClient, IpcDaemonTestRunClient>();
                services.AddSingleton<IUnityResultsXmlParser, UnityResultsXmlParser>();
                services.AddSingleton<IUnityResultsArtifactWriter, UnityResultsArtifactWriter>();
                services.AddSingleton<IUnityResultsConverter, UnityResultsConverter>();
                services.AddSingleton<ITestRunProfileLoader, TestRunProfileLoader>();
                services.AddSingleton<ITestRunConfigurationResolver, TestRunConfigurationResolver>();
                services.AddSingleton<ITestRunPreflightService, TestRunPreflightService>();
                services.AddSingleton<ITestRunExecutionPipeline, TestRunExecutionPipeline>();
                services.AddSingleton<ITestRunResultMapper, TestRunResultMapper>();
                services.AddSingleton<ITestRunService, TestRunService>();
                services.AddSingleton<IStatusExecutionContextResolver, StatusExecutionContextResolver>();
                services.AddSingleton<IStatusDaemonObservationService, StatusDaemonObservationService>();
                services.AddSingleton<IStatusService, StatusService>();
            });
        app.UseFilter<OperationCatalogWarmupFilter>();
        app.Add<InitCommand>();
        app.Add<StatusCommand>();
        app.Add<DaemonStartCommand>("daemon");
        app.Add<DaemonStopCommand>("daemon");
        app.Add<DaemonStatusCommand>("daemon");
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
        if (CommandTokenClassifier.IsHelpOptionToken(secondArgument))
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

}