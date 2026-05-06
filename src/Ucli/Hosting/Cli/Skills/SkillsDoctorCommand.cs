using ConsoleAppFramework;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Skills.Distribution;
using MackySoft.Ucli.Skills.Doctor;
using MackySoft.Ucli.Skills.Hosts.Registration;
using MackySoft.Ucli.Skills.Installation;

namespace MackySoft.Ucli.Hosting.Cli.Skills;

/// <summary> Provides the skills doctor CLI command entry point. </summary>
internal sealed class SkillsDoctorCommand
{
    private readonly OfficialSkillPackageProvider packageProvider;
    private readonly SkillHostAdapterSet hostAdapters;
    private readonly SkillInstallTargetResolver targetResolver;
    private readonly SkillDoctorService doctorService;
    private readonly ICommandResultWriter commandResultWriter;

    /// <summary> Initializes a new instance of the <see cref="SkillsDoctorCommand" /> class. </summary>
    /// <param name="packageProvider"> The official SKILL package provider. </param>
    /// <param name="hostAdapters"> The supported host adapter set. </param>
    /// <param name="targetResolver"> The SKILL install target resolver. </param>
    /// <param name="doctorService"> The SKILL doctor service. </param>
    /// <param name="commandResultWriter"> The command-result writer dependency. </param>
    public SkillsDoctorCommand (
        OfficialSkillPackageProvider packageProvider,
        SkillHostAdapterSet hostAdapters,
        SkillInstallTargetResolver targetResolver,
        SkillDoctorService doctorService,
        ICommandResultWriter commandResultWriter)
    {
        this.packageProvider = packageProvider ?? throw new ArgumentNullException(nameof(packageProvider));
        this.hostAdapters = hostAdapters ?? throw new ArgumentNullException(nameof(hostAdapters));
        this.targetResolver = targetResolver ?? throw new ArgumentNullException(nameof(targetResolver));
        this.doctorService = doctorService ?? throw new ArgumentNullException(nameof(doctorService));
        this.commandResultWriter = commandResultWriter ?? throw new ArgumentNullException(nameof(commandResultWriter));
    }

    /// <summary> Executes the skills doctor command and emits the JSON result contract. </summary>
    /// <param name="host"> Required target host (claude|copilot|openai). </param>
    /// <param name="scope"> Required install scope. Only project is supported. </param>
    /// <param name="repoRoot"> --repoRoot, Required repository root. </param>
    /// <param name="targetDir"> --targetDir, Optional target root path under the repository root. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(UcliCommandNames.DoctorSubcommand)]
    public async Task<int> Doctor (
        string? host = null,
        string? scope = null,
        string? repoRoot = null,
        string? targetDir = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommandExecutionState.MarkStarted();

        var normalizedHost = SkillsCommandOptionNormalizer.NormalizeHost(
            UcliCommandNames.SkillsDoctor,
            host,
            hostAdapters,
            out var errorResult);
        if (errorResult is not null)
        {
            commandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        var normalizedScope = SkillsCommandOptionNormalizer.NormalizeProjectScope(
            UcliCommandNames.SkillsDoctor,
            scope,
            out errorResult);
        if (errorResult is not null)
        {
            commandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        var repositoryRoot = SkillsCommandOptionNormalizer.NormalizeRequiredFullPath(
            UcliCommandNames.SkillsDoctor,
            "repoRoot",
            repoRoot,
            out errorResult);
        if (errorResult is not null)
        {
            commandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        var targetResult = targetResolver.ResolveTarget(new SkillInstallRequest(normalizedHost!, normalizedScope!.Value, repositoryRoot!, targetDir));
        if (!targetResult.IsSuccess)
        {
            var targetErrorResult = SkillsCommandResultFactory.CreateSkillFailure(UcliCommandNames.SkillsDoctor, targetResult.Failure!);
            commandResultWriter.WriteToStandardOutput(targetErrorResult);
            return targetErrorResult.ExitCode;
        }

        var packagesResult = await packageProvider.GetPackagesAsync(cancellationToken).ConfigureAwait(false);
        if (!packagesResult.IsSuccess)
        {
            var packageErrorResult = SkillsCommandResultFactory.CreateSkillFailure(UcliCommandNames.SkillsDoctor, packagesResult.Failure!);
            commandResultWriter.WriteToStandardOutput(packageErrorResult);
            return packageErrorResult.ExitCode;
        }

        var doctorResult = await doctorService.DiagnoseAsync(
                packagesResult.Value!,
                normalizedHost!,
                targetResult.Value!.TargetRoot,
                cancellationToken)
            .ConfigureAwait(false);
        var commandResult = SkillsCommandResultFactory.CreateDoctor(doctorResult, repositoryRoot!);
        commandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }
}
