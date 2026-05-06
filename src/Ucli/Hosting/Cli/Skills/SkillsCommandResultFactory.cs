using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Skills.Doctor;
using MackySoft.Ucli.Skills.Hosts.Registration;
using MackySoft.Ucli.Skills.Installation;
using MackySoft.Ucli.Skills.Packaging;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Hosting.Cli.Skills;

/// <summary> Creates command-level JSON results for <c>skills</c> commands. </summary>
internal static class SkillsCommandResultFactory
{
    private const string ProjectScopeLiteral = "project";

    /// <summary> Creates a successful command result for <c>skills list</c>. </summary>
    /// <param name="packages"> The official SKILL packages. </param>
    /// <param name="hostAdapters"> The supported host adapter set. </param>
    /// <returns> The command result serialized to stdout. </returns>
    public static CommandResult CreateList (
        IReadOnlyList<CanonicalSkillPackage> packages,
        SkillHostAdapterSet hostAdapters)
    {
        ArgumentNullException.ThrowIfNull(packages);
        ArgumentNullException.ThrowIfNull(hostAdapters);

        return CommandResult.Success(
            command: UcliCommandNames.SkillsList,
            message: "uCLI official SKILL package list retrieval completed.",
            payload: new
            {
                skills = packages
                    .OrderBy(static package => package.SkillName, StringComparer.Ordinal)
                    .Select(static package => new
                    {
                        package.SkillName,
                        package.DisplayName,
                        package.Description,
                        package.Manifest.ContentDigest,
                        package.Manifest.HostArtifacts,
                    })
                    .ToArray(),
                supportedHosts = hostAdapters.Adapters
                    .Select(static adapter => new
                    {
                        host = adapter.Descriptor.HostKey,
                        adapter.Descriptor.ProjectTargetDirectory,
                    })
                    .ToArray(),
            });
    }

    /// <summary> Creates a command result for <c>skills export</c>. </summary>
    /// <param name="result"> The export result. </param>
    /// <param name="packages"> The exported packages. </param>
    /// <param name="host"> The normalized host key. </param>
    /// <returns> The command result serialized to stdout. </returns>
    public static CommandResult CreateExport (
        SkillOperationResult<string> result,
        IReadOnlyList<CanonicalSkillPackage> packages,
        string host)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(packages);
        ArgumentException.ThrowIfNullOrWhiteSpace(host);

        if (!result.IsSuccess)
        {
            return CreateSkillFailure(UcliCommandNames.SkillsExport, result.Failure!);
        }

        return CommandResult.Success(
            command: UcliCommandNames.SkillsExport,
            message: "uCLI official SKILL packages exported.",
            payload: new
            {
                host,
                outputRoot = result.Value!,
                skills = CreateSkillNameList(packages),
                skillCount = packages.Count,
            });
    }

    /// <summary> Creates a command result for <c>skills install</c>. </summary>
    /// <param name="result"> The install result. </param>
    /// <param name="host"> The normalized host key. </param>
    /// <param name="repositoryRoot"> The canonical repository root. </param>
    /// <returns> The command result serialized to stdout. </returns>
    public static CommandResult CreateInstall (
        SkillOperationResult<SkillInstallResult> result,
        string host,
        string repositoryRoot)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);

        if (!result.IsSuccess)
        {
            return CreateSkillFailure(UcliCommandNames.SkillsInstall, result.Failure!);
        }

        var installResult = result.Value!;
        var actions = installResult.Actions
            .OrderBy(static action => action.Identity.SkillName, StringComparer.Ordinal)
            .Select(static action => new
            {
                action.Identity.SkillName,
                action = ToActionLiteral(action.ActionKind),
                action.Identity.TargetRoot,
            })
            .ToArray();

        return CommandResult.Success(
            command: UcliCommandNames.SkillsInstall,
            message: "uCLI official SKILL packages installed.",
            payload: new
            {
                host,
                scope = ProjectScopeLiteral,
                repositoryRoot,
                installResult.TargetRoot,
                actions,
                createdCount = installResult.Actions.Count(static action => action.ActionKind == SkillInstallActionKind.Created),
                noOpCount = installResult.Actions.Count(static action => action.ActionKind == SkillInstallActionKind.NoOp),
            });
    }

    /// <summary> Creates a command result for <c>skills update</c>. </summary>
    /// <param name="result"> The update result. </param>
    /// <param name="host"> The normalized host key. </param>
    /// <param name="repositoryRoot"> The canonical repository root. </param>
    /// <returns> The command result serialized to stdout. </returns>
    public static CommandResult CreateUpdate (
        SkillOperationResult<SkillUpdateResult> result,
        string host,
        string repositoryRoot)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);

        if (!result.IsSuccess)
        {
            return CreateSkillFailure(UcliCommandNames.SkillsUpdate, result.Failure!);
        }

        var updateResult = result.Value!;
        var actions = updateResult.Actions
            .OrderBy(static action => action.Identity.SkillName, StringComparer.Ordinal)
            .Select(static action => new
            {
                action.Identity.SkillName,
                action = ToActionLiteral(action.ActionKind),
                action.Identity.TargetRoot,
            })
            .ToArray();

        return CommandResult.Success(
            command: UcliCommandNames.SkillsUpdate,
            message: "uCLI official SKILL packages updated.",
            payload: new
            {
                host,
                scope = ProjectScopeLiteral,
                repositoryRoot,
                updateResult.TargetRoot,
                actions,
                createdCount = updateResult.Actions.Count(static action => action.ActionKind == SkillUpdateActionKind.Created),
                updatedCount = updateResult.Actions.Count(static action => action.ActionKind == SkillUpdateActionKind.Updated),
                noOpCount = updateResult.Actions.Count(static action => action.ActionKind == SkillUpdateActionKind.NoOp),
            });
    }

    /// <summary> Creates a command result for <c>skills uninstall</c>. </summary>
    /// <param name="result"> The uninstall result. </param>
    /// <param name="host"> The normalized host key. </param>
    /// <param name="repositoryRoot"> The canonical repository root. </param>
    /// <returns> The command result serialized to stdout. </returns>
    public static CommandResult CreateUninstall (
        SkillOperationResult<SkillUninstallResult> result,
        string host,
        string repositoryRoot)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);

        if (!result.IsSuccess)
        {
            return CreateSkillFailure(UcliCommandNames.SkillsUninstall, result.Failure!);
        }

        var uninstallResult = result.Value!;
        var actions = uninstallResult.Actions
            .OrderBy(static action => action.Identity.SkillName, StringComparer.Ordinal)
            .Select(static action => new
            {
                action.Identity.SkillName,
                action = ToActionLiteral(action.ActionKind),
                action.Identity.TargetRoot,
            })
            .ToArray();

        return CommandResult.Success(
            command: UcliCommandNames.SkillsUninstall,
            message: "uCLI official SKILL packages uninstalled.",
            payload: new
            {
                host,
                scope = ProjectScopeLiteral,
                repositoryRoot,
                uninstallResult.TargetRoot,
                actions,
                deletedCount = uninstallResult.Actions.Count(static action => action.ActionKind == SkillUninstallActionKind.Deleted),
                noOpCount = uninstallResult.Actions.Count(static action => action.ActionKind == SkillUninstallActionKind.NoOp),
                skippedUnmanagedCount = uninstallResult.Actions.Count(static action => action.ActionKind == SkillUninstallActionKind.SkippedUnmanaged),
            });
    }

    /// <summary> Creates a command result for <c>skills doctor</c>. </summary>
    /// <param name="result"> The doctor result. </param>
    /// <param name="repositoryRoot"> The canonical repository root. </param>
    /// <returns> The command result serialized to stdout. </returns>
    public static CommandResult CreateDoctor (
        SkillDoctorResult result,
        string repositoryRoot)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);

        var payload = new
        {
            host = result.Host,
            scope = ProjectScopeLiteral,
            repositoryRoot,
            result.TargetRoot,
            result.IsHealthy,
            diagnostics = result.Diagnostics
                .Select(static diagnostic => new
                {
                    severity = ToSeverityLiteral(diagnostic.Severity),
                    diagnostic.Code,
                    diagnostic.Message,
                    diagnostic.SkillName,
                })
                .ToArray(),
        };

        if (result.IsHealthy)
        {
            return CommandResult.Success(
                command: UcliCommandNames.SkillsDoctor,
                message: "uCLI official SKILL packages are healthy.",
                payload: payload);
        }

        var errorDiagnostics = result.Diagnostics
            .Where(static diagnostic => diagnostic.Severity == SkillDoctorSeverity.Error)
            .ToArray();
        var errors = errorDiagnostics.Length == 0
            ? [new CommandError(IpcErrorCodes.InternalError, "uCLI skills doctor reported an unknown error.", null)]
            : errorDiagnostics
                .Select(static diagnostic => new CommandError(diagnostic.Code, diagnostic.Message, null))
                .ToArray();

        return new CommandResult(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            Command: UcliCommandNames.SkillsDoctor,
            Status: IpcProtocol.StatusError,
            ExitCode: (int)CliExitCode.ToolError,
            Message: "uCLI skills doctor reported errors.",
            Payload: payload,
            Errors: errors);
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

        return new CommandResult(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            Command: command,
            Status: IpcProtocol.StatusError,
            ExitCode: (int)ResolveExitCode(failure.Code),
            Message: failure.Message,
            Payload: new { },
            Errors:
            [
                new CommandError(failure.Code, failure.Message, null),
            ]);
    }

    private static string[] CreateSkillNameList (IReadOnlyList<CanonicalSkillPackage> packages)
    {
        return packages
            .Select(static package => package.SkillName)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static CliExitCode ResolveExitCode (string code)
    {
        return code is SkillFailureCodes.HostUnsupported or SkillFailureCodes.PathUnsafe
            ? CliExitCode.InvalidArgument
            : CliExitCode.ToolError;
    }

    private static string ToActionLiteral (SkillInstallActionKind actionKind)
    {
        return actionKind switch
        {
            SkillInstallActionKind.Created => "created",
            SkillInstallActionKind.NoOp => "noOp",
            _ => actionKind.ToString(),
        };
    }

    private static string ToActionLiteral (SkillUpdateActionKind actionKind)
    {
        return actionKind switch
        {
            SkillUpdateActionKind.Created => "created",
            SkillUpdateActionKind.Updated => "updated",
            SkillUpdateActionKind.NoOp => "noOp",
            _ => actionKind.ToString(),
        };
    }

    private static string ToActionLiteral (SkillUninstallActionKind actionKind)
    {
        return actionKind switch
        {
            SkillUninstallActionKind.Deleted => "deleted",
            SkillUninstallActionKind.NoOp => "noOp",
            SkillUninstallActionKind.SkippedUnmanaged => "skippedUnmanaged",
            _ => actionKind.ToString(),
        };
    }

    private static string ToSeverityLiteral (SkillDoctorSeverity severity)
    {
        return severity switch
        {
            SkillDoctorSeverity.Info => "info",
            SkillDoctorSeverity.Error => "error",
            _ => severity.ToString(),
        };
    }
}
