using MackySoft.AgentSkills.Doctor;
using MackySoft.AgentSkills.Hosting.Commands;
using MackySoft.AgentSkills.OperationReports.Contracts;
using MackySoft.AgentSkills.OperationReports.Literals;
using MackySoft.AgentSkills.Shared;
using MackySoft.Ucli.Application.Shared.Execution;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Skills;

/// <summary> Creates command-level JSON results for <c>skills</c> commands. </summary>
internal static class SkillsCommandResultFactory
{
    private const string PrivateVarPath = "/private/var";
    private const string VarPath = "/var";

    /// <summary> Creates a command result from the shared Agent Skills command runtime result. </summary>
    public static CommandResult Create (AgentSkillsCommandResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (!result.IsSuccess)
        {
            return CreateSkillFailure(result.Command, result.Failure!);
        }

        return result.Payload switch
        {
            SkillListReport report => CommandResult.Success(
                result.Command,
                "uCLI official SKILL package list retrieval completed.",
                CreateListPayload(report)),
            SkillExportReport report => CommandResult.Success(
                result.Command,
                "uCLI official SKILL packages exported.",
                CreateExportPayload(report)),
            SkillOperationReport report => CommandResult.Success(
                result.Command,
                CreateOperationMessage(result.Command, report),
                CreateOperationPayload(result.Command, report)),
            SkillDoctorReport report => CreateDoctor(result.Command, report),
            _ => CommandFailureProjector.Create(
                result.Command,
                ApplicationFailure.InternalError($"Unsupported Agent Skills command payload: {result.Payload?.GetType().FullName ?? "(null)"}"),
                new { }),
        };
    }

    /// <summary> Creates one command failure from a SKILL library failure. </summary>
    /// <param name="command"> The command name. </param>
    /// <param name="failure"> The SKILL operation failure. </param>
    /// <returns> The command result serialized to stdout. </returns>
    public static CommandResult CreateSkillFailure (
        string command,
        SkillFailure failure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentNullException.ThrowIfNull(failure);

        return CommandFailureProjector.Create(
            command,
            SkillFailureApplicationFailureMapper.Map(failure),
            new { });
    }

    private static object CreateListPayload (SkillListReport report)
    {
        return new
        {
            categories = report.Categories,
            skillNames = report.SkillNames,
            availableCategories = report.AvailableCategories
                .Select(static category => new
                {
                    category = category.Category,
                    skillCount = category.SkillCount,
                })
                .ToArray(),
            skills = report.Skills
                .Select(static skill => new
                {
                    skillName = skill.SkillName,
                    skill.DisplayName,
                    skill.Description,
                    dependencies = skill.Dependencies,
                    category = skill.Category,
                    catalogId = skill.CatalogId,
                    skillBundleVersion = skill.SkillBundleVersion,
                    contentDigest = skill.ContentDigest,
                    hostArtifacts = skill.HostArtifacts,
                })
                .ToArray(),
            supportedHosts = report.SupportedHosts
                .Select(static host => new
                {
                    host = host.Host,
                    projectTargetDirectory = host.ProjectDefaultTargetPath,
                    userTargetDirectory = host.UserDefaultTargetPath,
                    host.ReloadGuidance,
                })
                .ToArray(),
        };
    }

    private static object CreateExportPayload (SkillExportReport report)
    {
        return new
        {
            host = report.Host,
            categories = report.Categories,
            skillNames = report.SkillNames,
            format = report.Format,
            outputRoot = ToDisplayPath(report.OutputPath),
            skills = report.Skills,
            skillCount = report.SkillCount,
            reloadGuidance = report.ReloadGuidance,
        };
    }

    private static object CreateOperationPayload (
        string command,
        SkillOperationReport report)
    {
        var targetRoot = ToDisplayPath(report.TargetRoot);
        var actions = CreateActionPayloads(report.Actions, targetRoot);
        var repositoryRoot = report.RepositoryRoot is null ? null : ToDisplayPath(report.RepositoryRoot);

        return command switch
        {
            UcliCommandNames.SkillsInstall => new
            {
                host = report.Host,
                categories = report.Categories,
                skillNames = report.SkillNames,
                scope = report.Scope,
                repositoryRoot,
                targetRoot,
                report.DryRun,
                report.Force,
                printDiff = HasDiffs(report),
                reloadGuidance = report.ReloadGuidance,
                actions,
                createdCount = CountAction(report, "created"),
                updatedCount = CountAction(report, "updated"),
                noOpCount = CountAction(report, "noOp"),
                blockedCount = CountBlocked(report),
            },
            UcliCommandNames.SkillsUpdate => new
            {
                host = report.Host,
                categories = report.Categories,
                skillNames = report.SkillNames,
                scope = report.Scope,
                repositoryRoot,
                targetRoot,
                report.DryRun,
                report.Force,
                printDiff = HasDiffs(report),
                reloadGuidance = report.ReloadGuidance,
                actions,
                createdCount = CountAction(report, "created"),
                updatedCount = CountAction(report, "updated"),
                noOpCount = CountAction(report, "noOp"),
                blockedCount = CountBlocked(report),
            },
            UcliCommandNames.SkillsUninstall => new
            {
                host = report.Host,
                categories = report.Categories,
                skillNames = report.SkillNames,
                scope = report.Scope,
                repositoryRoot,
                targetRoot,
                report.DryRun,
                report.Force,
                reloadGuidance = report.ReloadGuidance,
                actions,
                deletedCount = CountAction(report, "deleted"),
                noOpCount = CountAction(report, "noOp"),
                skippedUnmanagedCount = CountAction(report, "skippedUnmanaged"),
                blockedCount = CountBlocked(report),
            },
            UcliCommandNames.SkillsPrune => new
            {
                host = report.Host,
                categories = report.Categories,
                skillNames = report.SkillNames,
                scope = report.Scope,
                repositoryRoot,
                targetRoot,
                report.DryRun,
                report.Force,
                reloadGuidance = report.ReloadGuidance,
                actions,
                deletedCount = CountAction(report, "deleted"),
                skippedCurrentCount = CountAction(report, "skippedCurrent"),
                skippedForeignCatalogCount = CountAction(report, "skippedForeignCatalog"),
                skippedUnmanagedCount = CountAction(report, "skippedUnmanaged"),
                blockedCount = CountBlocked(report),
            },
            _ => new
            {
                host = report.Host,
                categories = report.Categories,
                skillNames = report.SkillNames,
                scope = report.Scope,
                repositoryRoot,
                targetRoot,
                report.DryRun,
                report.Force,
                reloadGuidance = report.ReloadGuidance,
                actions,
                actionCounts = report.ActionCounts,
                statusCounts = report.StatusCounts,
            },
        };
    }

    private static CommandResult CreateDoctor (
        string command,
        SkillDoctorReport report)
    {
        var targetRoot = ToDisplayPath(report.TargetRoot);
        var payload = new
        {
            host = report.Host,
            categories = report.Categories,
            skillNames = report.SkillNames,
            scope = report.Scope,
            repositoryRoot = report.RepositoryRoot is null ? null : ToDisplayPath(report.RepositoryRoot),
            targetRoot,
            reloadGuidance = report.ReloadGuidance,
            report.IsHealthy,
            diagnostics = report.Diagnostics
                .Select(static diagnostic => new
                {
                    severity = diagnostic.Severity,
                    diagnostic.Code,
                    diagnostic.Message,
                    diagnostic.SkillName,
                })
                .ToArray(),
        };

        if (report.IsHealthy)
        {
            return CommandResult.Success(
                command,
                "uCLI official SKILL packages are healthy.",
                payload);
        }

        var failures = report.Diagnostics
            .Where(static diagnostic => diagnostic.Severity == SkillDoctorSeverity.Error)
            .Select(static diagnostic => ApplicationFailure.InternalError(diagnostic.Message, new UcliCode(diagnostic.Code)))
            .ToArray();
        if (failures.Length == 0)
        {
            failures =
            [
                ApplicationFailure.InternalError("uCLI skills doctor reported an unknown error."),
            ];
        }

        return CommandFailureProjector.Create(
            command,
            "uCLI skills doctor reported errors.",
            payload,
            failures);
    }

    private static string CreateOperationMessage (
        string command,
        SkillOperationReport report)
    {
        return command switch
        {
            UcliCommandNames.SkillsInstall => report.DryRun ? "uCLI official SKILL install plan generated." : "uCLI official SKILL packages installed.",
            UcliCommandNames.SkillsUpdate => report.DryRun ? "uCLI official SKILL update plan generated." : "uCLI official SKILL packages updated.",
            UcliCommandNames.SkillsUninstall => report.DryRun ? "uCLI official SKILL uninstall plan generated." : "uCLI official SKILL packages uninstalled.",
            UcliCommandNames.SkillsPrune => report.DryRun ? "uCLI official SKILL prune plan generated." : "uCLI official SKILL packages pruned.",
            _ => "uCLI official SKILL operation completed.",
        };
    }

    private static object[] CreateActionPayloads (
        IReadOnlyList<SkillOperationActionReport> actions,
        string targetRoot)
    {
        return actions
            .Select(action => new
            {
                skillName = action.SkillName,
                action = action.Action,
                targetRoot,
                blockedReason = action.BlockedReason,
                diffs = CreateDiffPayloads(action.FileDiffs),
            })
            .ToArray();
    }

    private static object[] CreateDiffPayloads (IReadOnlyList<SkillOperationFileDiffReport> fileDiffs)
    {
        if (fileDiffs.Count == 0)
        {
            return [];
        }

        return
        [
            new
            {
                files = fileDiffs
                    .Select(static file => new
                    {
                        relativePath = file.RelativePath,
                        changeKind = file.ChangeKind,
                        beforeContent = file.BeforeContent,
                        afterContent = file.AfterContent,
                    })
                    .ToArray(),
            },
        ];
    }

    private static int CountAction (
        SkillOperationReport report,
        string action)
    {
        return report.Actions.Count(candidate => string.Equals(candidate.Action, action, StringComparison.Ordinal));
    }

    private static int CountBlocked (SkillOperationReport report)
    {
        return report.Actions.Count(static action =>
            action.Status == SkillOperationActionStatus.Blocked
            || action.BlockedReason is not null);
    }

    private static bool HasDiffs (SkillOperationReport report)
    {
        return report.Actions.Any(static action => action.FileDiffs.Count > 0);
    }

    private static string ToDisplayPath (string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return path;
        }

        if (string.Equals(path, PrivateVarPath, StringComparison.Ordinal))
        {
            return VarPath;
        }

        const string privateVarPrefix = PrivateVarPath + "/";
        return path.StartsWith(privateVarPrefix, StringComparison.Ordinal)
            ? VarPath + "/" + path[privateVarPrefix.Length..]
            : path;
    }
}
