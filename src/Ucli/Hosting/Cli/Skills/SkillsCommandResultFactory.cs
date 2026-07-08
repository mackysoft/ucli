using MackySoft.AgentSkills.Distribution;
using MackySoft.AgentSkills.Doctor;
using MackySoft.AgentSkills.Hosts.Registration;
using MackySoft.AgentSkills.Installation.Results;
using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.Packaging.Canonical;
using MackySoft.AgentSkills.Shared;
using MackySoft.Ucli.Application.Shared.Execution;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Skills;

/// <summary> Creates command-level JSON results for <c>skills</c> commands. </summary>
internal static class SkillsCommandResultFactory
{
    private const string ProjectScopeLiteral = "project";
    private const string UserScopeLiteral = "user";

    /// <summary> Creates a successful command result for <c>skills list</c>. </summary>
    /// <param name="catalog"> The official SKILL package catalog. </param>
    /// <param name="hostAdapters"> The supported host adapter set. </param>
    /// <returns> The command result serialized to stdout. </returns>
    public static CommandResult CreateList (
        SkillPackageCatalog catalog,
        SkillHostAdapterSet hostAdapters)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(hostAdapters);

        return CommandResult.Success(
            command: UcliCommandNames.SkillsList,
            message: "uCLI official SKILL package list retrieval completed.",
            payload: new
            {
                tiers = catalog.SelectedTiers.Select(static tier => tier.Value).ToArray(),
                skillNames = catalog.SelectedSkillNames.Select(static skillName => skillName.Value).ToArray(),
                availableTiers = catalog.AvailableTiers
                    .Select(static tier => new
                    {
                        tier = tier.Tier.Value,
                        skillCount = tier.PackageCount,
                    })
                    .ToArray(),
                skills = catalog.Packages
                    .OrderBy(static package => package.Manifest.SkillName.Value, StringComparer.Ordinal)
                    .Select(static package => new
                    {
                        skillName = package.Manifest.SkillName.Value,
                        package.Manifest.DisplayName,
                        package.Manifest.Description,
                        dependencies = package.Manifest.Dependencies.Select(static dependency => dependency.Value).ToArray(),
                        tier = package.Manifest.Tier.Value,
                        catalogId = package.Manifest.CatalogId.Value,
                        package.Manifest.SkillBundleVersion,
                        package.Manifest.ContentDigest,
                        package.Manifest.HostArtifacts,
                    })
                    .ToArray(),
                supportedHosts = hostAdapters.Adapters
                    .Select(static adapter => new
                    {
                        host = adapter.Descriptor.HostKey,
                        projectTargetDirectory = adapter.Descriptor.ProjectDefaultTargetPath,
                        userTargetDirectory = adapter.Descriptor.UserDefaultTargetPath,
                        adapter.Descriptor.ReloadGuidance,
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
        string host,
        SkillExportFormat format,
        string reloadGuidance,
        IReadOnlyList<string> tiers,
        IReadOnlyList<string> skillNames)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(packages);
        ArgumentNullException.ThrowIfNull(tiers);
        ArgumentNullException.ThrowIfNull(skillNames);
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentException.ThrowIfNullOrWhiteSpace(reloadGuidance);

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
                tiers,
                skillNames,
                format = ToExportFormatLiteral(format),
                outputRoot = result.Value!,
                skills = CreateSkillNameList(packages),
                skillCount = packages.Count,
                reloadGuidance,
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
        SkillScopeKind scope,
        string? repositoryRoot,
        string reloadGuidance,
        IReadOnlyList<string> tiers,
        IReadOnlyList<string> skillNames)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(tiers);
        ArgumentNullException.ThrowIfNull(skillNames);
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentException.ThrowIfNullOrWhiteSpace(reloadGuidance);

        if (!result.IsSuccess)
        {
            return CreateSkillFailure(UcliCommandNames.SkillsInstall, result.Failure!);
        }

        var installResult = result.Value!;
        var actions = CreateActionPayloads(
            installResult.Actions,
            static action => action.Identity,
            static action => ToActionLiteral(action.ActionKind),
            static action => action.BlockedReason,
            static action => action.Diffs);

        return CommandResult.Success(
            command: UcliCommandNames.SkillsInstall,
            message: installResult.DryRun ? "uCLI official SKILL install plan generated." : "uCLI official SKILL packages installed.",
            payload: new
            {
                host,
                tiers,
                skillNames,
                scope = ToScopeLiteral(scope),
                repositoryRoot,
                installResult.TargetRoot,
                installResult.DryRun,
                installResult.Force,
                installResult.PrintDiff,
                reloadGuidance,
                actions,
                createdCount = installResult.Actions.Count(static action => action.ActionKind == SkillInstallActionKind.Created),
                updatedCount = installResult.Actions.Count(static action => action.ActionKind == SkillInstallActionKind.Updated),
                noOpCount = installResult.Actions.Count(static action => action.ActionKind == SkillInstallActionKind.NoOp),
                blockedCount = installResult.Actions.Count(static action => IsBlocked(action.ActionKind)),
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
        SkillScopeKind scope,
        string? repositoryRoot,
        string reloadGuidance,
        IReadOnlyList<string> tiers,
        IReadOnlyList<string> skillNames)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(tiers);
        ArgumentNullException.ThrowIfNull(skillNames);
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentException.ThrowIfNullOrWhiteSpace(reloadGuidance);

        if (!result.IsSuccess)
        {
            return CreateSkillFailure(UcliCommandNames.SkillsUpdate, result.Failure!);
        }

        var updateResult = result.Value!;
        var actions = CreateActionPayloads(
            updateResult.Actions,
            static action => action.Identity,
            static action => ToActionLiteral(action.ActionKind),
            static action => action.BlockedReason,
            static action => action.Diffs);

        return CommandResult.Success(
            command: UcliCommandNames.SkillsUpdate,
            message: updateResult.DryRun ? "uCLI official SKILL update plan generated." : "uCLI official SKILL packages updated.",
            payload: new
            {
                host,
                tiers,
                skillNames,
                scope = ToScopeLiteral(scope),
                repositoryRoot,
                updateResult.TargetRoot,
                updateResult.DryRun,
                updateResult.Force,
                updateResult.PrintDiff,
                reloadGuidance,
                actions,
                createdCount = updateResult.Actions.Count(static action => action.ActionKind == SkillUpdateActionKind.Created),
                updatedCount = updateResult.Actions.Count(static action => action.ActionKind == SkillUpdateActionKind.Updated),
                noOpCount = updateResult.Actions.Count(static action => action.ActionKind == SkillUpdateActionKind.NoOp),
                blockedCount = updateResult.Actions.Count(static action => IsBlocked(action.ActionKind)),
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
        SkillScopeKind scope,
        string? repositoryRoot,
        string reloadGuidance,
        IReadOnlyList<string> tiers,
        IReadOnlyList<string> skillNames)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(tiers);
        ArgumentNullException.ThrowIfNull(skillNames);
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentException.ThrowIfNullOrWhiteSpace(reloadGuidance);

        if (!result.IsSuccess)
        {
            return CreateSkillFailure(UcliCommandNames.SkillsUninstall, result.Failure!);
        }

        var uninstallResult = result.Value!;
        var actions = CreateActionPayloads(
            uninstallResult.Actions,
            static action => action.Identity,
            static action => ToActionLiteral(action.ActionKind),
            static action => action.BlockedReason,
            static _ => null);

        return CommandResult.Success(
            command: UcliCommandNames.SkillsUninstall,
            message: uninstallResult.DryRun ? "uCLI official SKILL uninstall plan generated." : "uCLI official SKILL packages uninstalled.",
            payload: new
            {
                host,
                tiers,
                skillNames,
                scope = ToScopeLiteral(scope),
                repositoryRoot,
                uninstallResult.TargetRoot,
                uninstallResult.DryRun,
                uninstallResult.Force,
                reloadGuidance,
                actions,
                deletedCount = uninstallResult.Actions.Count(static action => action.ActionKind == SkillUninstallActionKind.Deleted),
                noOpCount = uninstallResult.Actions.Count(static action => action.ActionKind == SkillUninstallActionKind.NoOp),
                skippedUnmanagedCount = uninstallResult.Actions.Count(static action => action.ActionKind == SkillUninstallActionKind.SkippedUnmanaged),
                blockedCount = uninstallResult.Actions.Count(static action => IsBlocked(action.ActionKind)),
            });
    }

    /// <summary> Creates a command result for <c>skills prune</c>. </summary>
    /// <param name="result"> The prune result. </param>
    /// <param name="host"> The normalized host key. </param>
    /// <param name="repositoryRoot"> The canonical repository root. </param>
    /// <returns> The command result serialized to stdout. </returns>
    public static CommandResult CreatePrune (
        SkillOperationResult<SkillPruneResult> result,
        string host,
        SkillScopeKind scope,
        string? repositoryRoot,
        string reloadGuidance,
        IReadOnlyList<string> tiers,
        IReadOnlyList<string> skillNames)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(tiers);
        ArgumentNullException.ThrowIfNull(skillNames);
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentException.ThrowIfNullOrWhiteSpace(reloadGuidance);

        if (!result.IsSuccess)
        {
            return CreateSkillFailure(UcliCommandNames.SkillsPrune, result.Failure!);
        }

        var pruneResult = result.Value!;
        var actions = CreateActionPayloads(
            pruneResult.Actions,
            static action => action.Identity,
            static action => ToActionLiteral(action.ActionKind),
            static action => action.BlockedReason,
            static _ => null);

        return CommandResult.Success(
            command: UcliCommandNames.SkillsPrune,
            message: pruneResult.DryRun ? "uCLI official SKILL prune plan generated." : "uCLI official SKILL packages pruned.",
            payload: new
            {
                host,
                tiers,
                skillNames,
                scope = ToScopeLiteral(scope),
                repositoryRoot,
                pruneResult.TargetRoot,
                pruneResult.DryRun,
                pruneResult.Force,
                reloadGuidance,
                actions,
                deletedCount = pruneResult.Actions.Count(static action => action.ActionKind == SkillPruneActionKind.Deleted),
                skippedCurrentCount = pruneResult.Actions.Count(static action => action.ActionKind == SkillPruneActionKind.SkippedCurrent),
                skippedForeignCatalogCount = pruneResult.Actions.Count(static action => action.ActionKind == SkillPruneActionKind.SkippedForeignCatalog),
                skippedUnmanagedCount = pruneResult.Actions.Count(static action => action.ActionKind == SkillPruneActionKind.SkippedUnmanaged),
                blockedCount = pruneResult.Actions.Count(static action => IsBlocked(action.ActionKind)),
            });
    }

    /// <summary> Creates a command result for <c>skills doctor</c>. </summary>
    /// <param name="result"> The doctor result. </param>
    /// <param name="repositoryRoot"> The canonical repository root. </param>
    /// <returns> The command result serialized to stdout. </returns>
    public static CommandResult CreateDoctor (
        SkillDoctorResult result,
        SkillScopeKind scope,
        string? repositoryRoot,
        string reloadGuidance,
        IReadOnlyList<string> tiers,
        IReadOnlyList<string> skillNames)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(tiers);
        ArgumentNullException.ThrowIfNull(skillNames);
        ArgumentException.ThrowIfNullOrWhiteSpace(reloadGuidance);

        var payload = new
        {
            host = result.Host,
            tiers,
            skillNames,
            scope = ToScopeLiteral(scope),
            repositoryRoot,
            result.TargetRoot,
            reloadGuidance,
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
            ? [ApplicationFailure.InternalError("uCLI skills doctor reported an unknown error.")]
            : errorDiagnostics
                .Select(static diagnostic => ApplicationFailure.InternalError(diagnostic.Message, new UcliCode(diagnostic.Code)))
                .ToArray();

        return CommandFailureProjector.Create(
            UcliCommandNames.SkillsDoctor,
            "uCLI skills doctor reported errors.",
            payload,
            errors);
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

    private static string[] CreateSkillNameList (IReadOnlyList<CanonicalSkillPackage> packages)
    {
        return packages
            .Select(static package => package.Manifest.SkillName.Value)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string ToScopeLiteral (SkillScopeKind scope)
    {
        return scope switch
        {
            SkillScopeKind.Project => ProjectScopeLiteral,
            SkillScopeKind.User => UserScopeLiteral,
            _ => scope.ToString(),
        };
    }

    private static string ToExportFormatLiteral (SkillExportFormat format)
    {
        return format switch
        {
            SkillExportFormat.Directory => "directory",
            SkillExportFormat.Zip => "zip",
            _ => format.ToString(),
        };
    }

    private static string ToActionLiteral (SkillInstallActionKind actionKind)
    {
        return actionKind switch
        {
            SkillInstallActionKind.Created => "created",
            SkillInstallActionKind.Updated => "updated",
            SkillInstallActionKind.NoOp => "noOp",
            SkillInstallActionKind.BlockedManagedOverwrite => "blockedManagedOverwrite",
            SkillInstallActionKind.BlockedLocalModification => "blockedLocalModification",
            SkillInstallActionKind.BlockedUnmanaged => "blockedUnmanaged",
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
            SkillUpdateActionKind.BlockedLocalModification => "blockedLocalModification",
            SkillUpdateActionKind.BlockedUnmanaged => "blockedUnmanaged",
            SkillUpdateActionKind.BlockedVersionAhead => "blockedVersionAhead",
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
            SkillUninstallActionKind.BlockedLocalModification => "blockedLocalModification",
            _ => actionKind.ToString(),
        };
    }

    private static string ToActionLiteral (SkillPruneActionKind actionKind)
    {
        return actionKind switch
        {
            SkillPruneActionKind.Deleted => "deleted",
            SkillPruneActionKind.SkippedCurrent => "skippedCurrent",
            SkillPruneActionKind.SkippedForeignCatalog => "skippedForeignCatalog",
            SkillPruneActionKind.SkippedUnmanaged => "skippedUnmanaged",
            SkillPruneActionKind.BlockedLocalModification => "blockedLocalModification",
            SkillPruneActionKind.BlockedManifestInvalid => "blockedManifestInvalid",
            SkillPruneActionKind.BlockedNameCollision => "blockedNameCollision",
            SkillPruneActionKind.BlockedHostConflict => "blockedHostConflict",
            _ => actionKind.ToString(),
        };
    }

    private static bool IsBlocked (SkillInstallActionKind actionKind)
    {
        return actionKind is SkillInstallActionKind.BlockedManagedOverwrite
            or SkillInstallActionKind.BlockedLocalModification
            or SkillInstallActionKind.BlockedUnmanaged;
    }

    private static bool IsBlocked (SkillUpdateActionKind actionKind)
    {
        return actionKind is SkillUpdateActionKind.BlockedLocalModification
            or SkillUpdateActionKind.BlockedUnmanaged
            or SkillUpdateActionKind.BlockedVersionAhead;
    }

    private static bool IsBlocked (SkillUninstallActionKind actionKind)
    {
        return actionKind is SkillUninstallActionKind.BlockedLocalModification;
    }

    private static bool IsBlocked (SkillPruneActionKind actionKind)
    {
        return actionKind is SkillPruneActionKind.BlockedLocalModification
            or SkillPruneActionKind.BlockedManifestInvalid
            or SkillPruneActionKind.BlockedNameCollision
            or SkillPruneActionKind.BlockedHostConflict;
    }

    private static object[] CreateActionPayloads<TAction> (
        IReadOnlyList<TAction> actions,
        Func<TAction, SkillInstallIdentity> getIdentity,
        Func<TAction, string> getActionLiteral,
        Func<TAction, SkillBlockedReason?> getBlockedReason,
        Func<TAction, IReadOnlyList<SkillActionDiff>?> getDiffs)
    {
        return actions
            .OrderBy(action => getIdentity(action).SkillName.Value, StringComparer.Ordinal)
            .Select(action =>
            {
                var identity = getIdentity(action);
                return new
                {
                    skillName = identity.SkillName.Value,
                    action = getActionLiteral(action),
                    identity.TargetRoot,
                    blockedReason = ToBlockedReasonLiteral(getBlockedReason(action)),
                    diffs = CreateDiffPayloads(getDiffs(action)),
                };
            })
            .ToArray();
    }

    private static string? ToBlockedReasonLiteral (SkillBlockedReason? reason)
    {
        return reason switch
        {
            null => null,
            SkillBlockedReason.ManagedOverwriteRequiresForce => "managedOverwriteRequiresForce",
            SkillBlockedReason.LocalModificationRequiresForce => "localModificationRequiresForce",
            SkillBlockedReason.UnmanagedTarget => "unmanagedTarget",
            SkillBlockedReason.InstalledVersionAhead => "installedVersionAhead",
            _ => reason.Value.ToString(),
        };
    }

    private static object[] CreateDiffPayloads (IReadOnlyList<SkillActionDiff>? diffs)
    {
        return (diffs ?? Array.Empty<SkillActionDiff>())
            .Select(static diff => new
            {
                files = diff.Files
                    .Select(static file => new
                    {
                        relativePath = file.RelativePath,
                        changeKind = ToDiffChangeKindLiteral(file.ChangeKind),
                        beforeContent = file.BeforeContent,
                        afterContent = file.AfterContent,
                    })
                    .ToArray(),
            })
            .ToArray();
    }

    private static string ToDiffChangeKindLiteral (SkillDiffChangeKind changeKind)
    {
        return changeKind switch
        {
            SkillDiffChangeKind.Added => "added",
            SkillDiffChangeKind.Modified => "modified",
            SkillDiffChangeKind.Deleted => "deleted",
            _ => changeKind.ToString(),
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
