using ConsoleAppFramework;
using MackySoft.Ucli.Hosting.Cli.Daemon;
using MackySoft.Ucli.Hosting.Cli.Daemon.Logs;
using MackySoft.Ucli.Hosting.Cli.Init;
using MackySoft.Ucli.Hosting.Cli.Ops;
using MackySoft.Ucli.Hosting.Cli.Requests;
using MackySoft.Ucli.Hosting.Cli.Skills;
using MackySoft.Ucli.Hosting.Cli.Status;
using MackySoft.Ucli.Hosting.Cli.Testing;

namespace MackySoft.Ucli.Hosting.Cli.Common.Startup;

/// <summary> Registers CLI command entrypoints with the application builder. </summary>
internal static class CliCommandRegistrar
{
    /// <summary> Registers all supported uCLI commands. </summary>
    /// <param name="app"> The application builder used to register commands. </param>
    /// <returns> The same <paramref name="app" /> instance for call chaining. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="app" /> is <see langword="null" />. </exception>
    public static ConsoleApp.ConsoleAppBuilder RegisterUcliCommands (this ConsoleApp.ConsoleAppBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.Add<InitCommand>();
        app.Add<StatusCommand>();
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
        app.Add<DaemonStartCommand>("daemon");
        app.Add<DaemonStopCommand>("daemon");
        app.Add<DaemonCleanupCommand>("daemon");
        app.Add<DaemonStatusCommand>("daemon");
        app.Add<DaemonListCommand>("daemon");
        app.Add<LogsDaemonCommand>("logs");
        app.Add<LogsUnityCommand>("logs");
        app.Add<OpsListCommand>("ops");
        app.Add<OpsDescribeCommand>("ops");
        app.Add<SkillsListCommand>("skills");
        app.Add<SkillsExportCommand>("skills");
        app.Add<SkillsInstallCommand>("skills");
        app.Add<SkillsDoctorCommand>("skills");
        app.Add<TestRunCommand>("test");
        app.Add<TestProfileInitCommand>("test profile");
        return app;
    }
}
