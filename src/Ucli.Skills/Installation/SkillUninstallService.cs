using MackySoft.Ucli.Skills.Packaging;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Uninstalls official SKILL packages from a host target root. </summary>
public sealed class SkillUninstallService
{
    private readonly SkillInstallTargetResolver targetResolver;
    private readonly SkillInstalledTargetStateAnalyzer targetStateAnalyzer;
    private readonly ISkillInstalledPackageRemover packageRemover;

    /// <summary> Initializes a new instance of the <see cref="SkillUninstallService" /> class. </summary>
    /// <param name="targetResolver"> The target resolver. </param>
    /// <param name="targetStateAnalyzer"> The installed target state analyzer. </param>
    /// <param name="packageRemover"> The installed package remover. </param>
    public SkillUninstallService (
        SkillInstallTargetResolver targetResolver,
        SkillInstalledTargetStateAnalyzer targetStateAnalyzer,
        ISkillInstalledPackageRemover packageRemover)
    {
        this.targetResolver = targetResolver ?? throw new ArgumentNullException(nameof(targetResolver));
        this.targetStateAnalyzer = targetStateAnalyzer ?? throw new ArgumentNullException(nameof(targetStateAnalyzer));
        this.packageRemover = packageRemover ?? throw new ArgumentNullException(nameof(packageRemover));
    }

    /// <summary> Uninstalls official SKILL packages. </summary>
    /// <param name="input"> The uninstall input. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The uninstall result or failure. </returns>
    public async ValueTask<SkillOperationResult<SkillUninstallResult>> UninstallAsync (
        SkillUninstallInput input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Packages);
        ArgumentNullException.ThrowIfNull(input.TargetRequest);

        var targetRequest = input.TargetRequest;
        var targetResult = targetResolver.ResolveTarget(targetRequest);
        if (!targetResult.IsSuccess)
        {
            return SkillOperationResult<SkillUninstallResult>.FailureResult(targetResult.Failure!.Code, targetResult.Failure.Message);
        }

        var target = targetResult.Value!;
        var targetRoot = target.TargetRoot;
        var actionPlans = new List<SkillUninstallActionPlan>();
        foreach (var package in input.Packages.OrderBy(static package => package.Manifest.SkillName, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var skillName = package.Manifest.SkillName;
            var skillDirectoryResult = SkillPackagePathBoundary.ResolvePackageDirectory(targetRoot, skillName);
            if (!skillDirectoryResult.IsSuccess)
            {
                return SkillOperationResult<SkillUninstallResult>.FailureResult(skillDirectoryResult.Failure!.Code, skillDirectoryResult.Failure.Message);
            }

            var skillDirectory = skillDirectoryResult.Value!;
            var identity = new SkillInstallIdentity(target.Host, targetRequest.Scope, targetRoot, skillName);
            var stateResult = await targetStateAnalyzer.AnalyzeAsync(package, skillDirectory, target.Host, cancellationToken).ConfigureAwait(false);
            if (!stateResult.IsSuccess)
            {
                return SkillOperationResult<SkillUninstallResult>.FailureResult(stateResult.Failure!.Code, stateResult.Failure.Message);
            }

            var actionPlanResult = CreateActionPlan(skillDirectory, identity, stateResult.Value!, input);
            if (!actionPlanResult.IsSuccess)
            {
                return SkillOperationResult<SkillUninstallResult>.FailureResult(actionPlanResult.Failure!.Code, actionPlanResult.Failure.Message);
            }

            actionPlans.Add(actionPlanResult.Value!);
        }

        if (!input.DryRun)
        {
            foreach (var actionPlan in actionPlans)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!actionPlan.ShouldDelete)
                {
                    continue;
                }

                var deleteResult = await packageRemover.DeleteAsync(targetRoot, actionPlan.SkillDirectory, cancellationToken).ConfigureAwait(false);
                if (!deleteResult.IsSuccess)
                {
                    return SkillOperationResult<SkillUninstallResult>.FailureResult(deleteResult.Failure!.Code, deleteResult.Failure.Message);
                }
            }
        }

        return SkillOperationResult<SkillUninstallResult>.Success(new SkillUninstallResult(
            targetRoot,
            actionPlans.Select(static actionPlan => actionPlan.Action).ToArray(),
            input.DryRun,
            input.Force));
    }

    private static SkillOperationResult<SkillUninstallActionPlan> CreateActionPlan (
        string skillDirectory,
        SkillInstallIdentity identity,
        SkillInstalledTargetState state,
        SkillUninstallInput input)
    {
        switch (state.Kind)
        {
            case SkillInstalledTargetStateKind.Missing:
                return SkillOperationResult<SkillUninstallActionPlan>.Success(new SkillUninstallActionPlan(
                    new SkillUninstallAction(identity, SkillUninstallActionKind.NoOp),
                    skillDirectory,
                    ShouldDelete: false));
            case SkillInstalledTargetStateKind.Current:
            case SkillInstalledTargetStateKind.CleanOutdated:
                return SkillOperationResult<SkillUninstallActionPlan>.Success(new SkillUninstallActionPlan(
                    new SkillUninstallAction(identity, SkillUninstallActionKind.Deleted),
                    skillDirectory,
                    ShouldDelete: true));
            case SkillInstalledTargetStateKind.Unmanaged:
                return SkillOperationResult<SkillUninstallActionPlan>.Success(new SkillUninstallActionPlan(
                    new SkillUninstallAction(identity, SkillUninstallActionKind.SkippedUnmanaged),
                    skillDirectory,
                    ShouldDelete: false));
            case SkillInstalledTargetStateKind.LocalModified:
                return CreateLocalModificationActionPlan(skillDirectory, identity, input);
            default:
                throw new ArgumentOutOfRangeException(nameof(state), state.Kind, "Unsupported target state.");
        }
    }

    private static SkillOperationResult<SkillUninstallActionPlan> CreateLocalModificationActionPlan (
        string skillDirectory,
        SkillInstallIdentity identity,
        SkillUninstallInput input)
    {
        if (input.Force)
        {
            return SkillOperationResult<SkillUninstallActionPlan>.Success(new SkillUninstallActionPlan(
                new SkillUninstallAction(identity, SkillUninstallActionKind.Deleted),
                skillDirectory,
                ShouldDelete: true));
        }

        if (input.DryRun)
        {
            return SkillOperationResult<SkillUninstallActionPlan>.Success(new SkillUninstallActionPlan(
                new SkillUninstallAction(
                    identity,
                    SkillUninstallActionKind.BlockedLocalModification,
                    SkillBlockedReason.LocalModificationRequiresForce),
                skillDirectory,
                ShouldDelete: false));
        }

        return SkillOperationResult<SkillUninstallActionPlan>.FailureResult(
            SkillFailureCodes.InstallTargetDigestMismatch,
            $"Target skill directory contains local modifications. Use --force to delete: {skillDirectory}");
    }

    private sealed record SkillUninstallActionPlan (
        SkillUninstallAction Action,
        string SkillDirectory,
        bool ShouldDelete);
}
