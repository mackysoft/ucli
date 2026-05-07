using MackySoft.Ucli.Skills.Materialization;
using MackySoft.Ucli.Skills.Packaging;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Updates official SKILL packages under a host target root. </summary>
public sealed class SkillUpdateService
{
    private readonly SkillInstallTargetResolver targetResolver;
    private readonly SkillMaterializationService materializationService;
    private readonly SkillInstalledTargetStateAnalyzer targetStateAnalyzer;
    private readonly ISkillMaterializedPackageWriter packageWriter;
    private readonly SkillMaterializedPackageDiffBuilder diffBuilder;

    /// <summary> Initializes a new instance of the <see cref="SkillUpdateService" /> class. </summary>
    /// <param name="targetResolver"> The target resolver. </param>
    /// <param name="materializationService"> The materialization service. </param>
    /// <param name="targetStateAnalyzer"> The installed target state analyzer. </param>
    /// <param name="packageWriter"> The materialized package writer. </param>
    /// <param name="diffBuilder"> The structured diff builder. </param>
    public SkillUpdateService (
        SkillInstallTargetResolver targetResolver,
        SkillMaterializationService materializationService,
        SkillInstalledTargetStateAnalyzer targetStateAnalyzer,
        ISkillMaterializedPackageWriter packageWriter,
        SkillMaterializedPackageDiffBuilder diffBuilder)
    {
        this.targetResolver = targetResolver ?? throw new ArgumentNullException(nameof(targetResolver));
        this.materializationService = materializationService ?? throw new ArgumentNullException(nameof(materializationService));
        this.targetStateAnalyzer = targetStateAnalyzer ?? throw new ArgumentNullException(nameof(targetStateAnalyzer));
        this.packageWriter = packageWriter ?? throw new ArgumentNullException(nameof(packageWriter));
        this.diffBuilder = diffBuilder ?? throw new ArgumentNullException(nameof(diffBuilder));
    }

    /// <summary> Updates official SKILL packages. </summary>
    /// <param name="input"> The update input. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The update result or failure. </returns>
    public async ValueTask<SkillOperationResult<SkillUpdateResult>> UpdateAsync (
        SkillUpdateInput input,
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
            return SkillOperationResult<SkillUpdateResult>.FailureResult(targetResult.Failure!.Code, targetResult.Failure.Message);
        }

        var target = targetResult.Value!;
        var targetRoot = target.TargetRoot;
        var actionPlans = new List<SkillUpdateActionPlan>();
        foreach (var package in input.Packages.OrderBy(static package => package.Manifest.SkillName, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var skillName = package.Manifest.SkillName;
            var skillDirectoryResult = SkillPackagePathBoundary.ResolvePackageDirectory(targetRoot, skillName);
            if (!skillDirectoryResult.IsSuccess)
            {
                return SkillOperationResult<SkillUpdateResult>.FailureResult(skillDirectoryResult.Failure!.Code, skillDirectoryResult.Failure.Message);
            }

            var skillDirectory = skillDirectoryResult.Value!;
            var identity = new SkillInstallIdentity(target.Host, targetRequest.Scope, targetRoot, skillName);
            var stateResult = await targetStateAnalyzer.AnalyzeAsync(package, skillDirectory, target.Host, cancellationToken).ConfigureAwait(false);
            if (!stateResult.IsSuccess)
            {
                return SkillOperationResult<SkillUpdateResult>.FailureResult(stateResult.Failure!.Code, stateResult.Failure.Message);
            }

            var actionPlanResult = await CreateActionPlanAsync(
                    package,
                    target.Host,
                    skillDirectory,
                    identity,
                    stateResult.Value!,
                    input,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!actionPlanResult.IsSuccess)
            {
                return SkillOperationResult<SkillUpdateResult>.FailureResult(actionPlanResult.Failure!.Code, actionPlanResult.Failure.Message);
            }

            actionPlans.Add(actionPlanResult.Value!);
        }

        if (!input.DryRun)
        {
            foreach (var actionPlan in actionPlans)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (actionPlan.MaterializedPackage is null)
                {
                    continue;
                }

                var preconditionResult = await ValidateWritePreconditionAsync(
                        actionPlan.Package,
                        target.Host,
                        actionPlan.SkillDirectory,
                        actionPlan.Action.ActionKind,
                        input.Force,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (!preconditionResult.IsSuccess)
                {
                    return SkillOperationResult<SkillUpdateResult>.FailureResult(preconditionResult.Failure!.Code, preconditionResult.Failure.Message);
                }
            }

            foreach (var actionPlan in actionPlans)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (actionPlan.MaterializedPackage is null)
                {
                    continue;
                }

                var writeResult = await packageWriter.WriteAsync(
                        targetRoot,
                        actionPlan.SkillDirectory,
                        actionPlan.MaterializedPackage,
                        ResolveWriteMode(actionPlan.Action.ActionKind),
                        (directory, token) => ValidateWritePreconditionAsync(
                            actionPlan.Package,
                            target.Host,
                            directory,
                            actionPlan.Action.ActionKind,
                            input.Force,
                            token),
                        cancellationToken)
                    .ConfigureAwait(false);
                if (!writeResult.IsSuccess)
                {
                    return SkillOperationResult<SkillUpdateResult>.FailureResult(writeResult.Failure!.Code, writeResult.Failure.Message);
                }
            }
        }

        return SkillOperationResult<SkillUpdateResult>.Success(new SkillUpdateResult(
            targetRoot,
            actionPlans.Select(static actionPlan => actionPlan.Action).ToArray(),
            input.DryRun,
            input.Force,
            input.PrintDiff));
    }

    private async ValueTask<SkillOperationResult<SkillUpdateActionPlan>> CreateActionPlanAsync (
        CanonicalSkillPackage package,
        string host,
        string skillDirectory,
        SkillInstallIdentity identity,
        SkillInstalledTargetState state,
        SkillUpdateInput input,
        CancellationToken cancellationToken)
    {
        switch (state.Kind)
        {
            case SkillInstalledTargetStateKind.Missing:
                return await CreateWriteActionPlanAsync(
                        package,
                        host,
                        skillDirectory,
                        identity,
                        SkillUpdateActionKind.Created,
                        input,
                        cancellationToken)
                    .ConfigureAwait(false);
            case SkillInstalledTargetStateKind.Current:
                return SkillOperationResult<SkillUpdateActionPlan>.Success(new SkillUpdateActionPlan(
                    new SkillUpdateAction(identity, SkillUpdateActionKind.NoOp),
                    skillDirectory,
                    package,
                    null));
            case SkillInstalledTargetStateKind.CleanOutdated:
                return await CreateWriteActionPlanAsync(
                        package,
                        host,
                        skillDirectory,
                        identity,
                        SkillUpdateActionKind.Updated,
                        input,
                        cancellationToken)
                    .ConfigureAwait(false);
            case SkillInstalledTargetStateKind.LocalModified:
                return await CreateLocalModificationActionPlanAsync(
                        package,
                        host,
                        skillDirectory,
                        identity,
                        input,
                        cancellationToken)
                    .ConfigureAwait(false);
            case SkillInstalledTargetStateKind.Unmanaged:
                if (!input.DryRun)
                {
                    return SkillOperationResult<SkillUpdateActionPlan>.FailureResult(
                        SkillFailureCodes.InstallTargetUnmanaged,
                        $"Target skill directory is not managed by uCLI: {skillDirectory}");
                }

                return await CreateBlockedActionPlanAsync(
                        package,
                        host,
                        skillDirectory,
                        identity,
                        SkillUpdateActionKind.BlockedUnmanaged,
                        SkillBlockedReason.UnmanagedTarget,
                        printDiff: false,
                        cancellationToken)
                    .ConfigureAwait(false);
            default:
                throw new ArgumentOutOfRangeException(nameof(state), state.Kind, "Unsupported target state.");
        }
    }

    private async ValueTask<SkillOperationResult<SkillUpdateActionPlan>> CreateLocalModificationActionPlanAsync (
        CanonicalSkillPackage package,
        string host,
        string skillDirectory,
        SkillInstallIdentity identity,
        SkillUpdateInput input,
        CancellationToken cancellationToken)
    {
        if (input.Force)
        {
            return await CreateWriteActionPlanAsync(
                    package,
                    host,
                    skillDirectory,
                    identity,
                    SkillUpdateActionKind.Updated,
                    input,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (input.DryRun)
        {
            return await CreateBlockedActionPlanAsync(
                    package,
                    host,
                    skillDirectory,
                    identity,
                    SkillUpdateActionKind.BlockedLocalModification,
                    SkillBlockedReason.LocalModificationRequiresForce,
                    input.PrintDiff,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return SkillOperationResult<SkillUpdateActionPlan>.FailureResult(
            SkillFailureCodes.InstallTargetDigestMismatch,
            $"Target skill directory contains local modifications. Use --force to overwrite: {skillDirectory}");
    }

    private async ValueTask<SkillOperationResult<SkillUpdateActionPlan>> CreateWriteActionPlanAsync (
        CanonicalSkillPackage package,
        string host,
        string skillDirectory,
        SkillInstallIdentity identity,
        SkillUpdateActionKind actionKind,
        SkillUpdateInput input,
        CancellationToken cancellationToken)
    {
        var packagePlanResult = await CreateMaterializedPackagePlanAsync(package, host, skillDirectory, input.PrintDiff, cancellationToken).ConfigureAwait(false);
        if (!packagePlanResult.IsSuccess)
        {
            return SkillOperationResult<SkillUpdateActionPlan>.FailureResult(packagePlanResult.Failure!.Code, packagePlanResult.Failure.Message);
        }

        var packagePlan = packagePlanResult.Value!;
        return SkillOperationResult<SkillUpdateActionPlan>.Success(new SkillUpdateActionPlan(
            new SkillUpdateAction(
                identity,
                actionKind,
                null,
                packagePlan.Diffs),
            skillDirectory,
            package,
            packagePlan.MaterializedPackage));
    }

    private async ValueTask<SkillOperationResult<SkillUpdateActionPlan>> CreateBlockedActionPlanAsync (
        CanonicalSkillPackage package,
        string host,
        string skillDirectory,
        SkillInstallIdentity identity,
        SkillUpdateActionKind actionKind,
        SkillBlockedReason blockedReason,
        bool printDiff,
        CancellationToken cancellationToken)
    {
        var packagePlanResult = await CreateMaterializedPackagePlanAsync(package, host, skillDirectory, printDiff, cancellationToken).ConfigureAwait(false);
        return packagePlanResult.IsSuccess
            ? SkillOperationResult<SkillUpdateActionPlan>.Success(new SkillUpdateActionPlan(
                new SkillUpdateAction(identity, actionKind, blockedReason, packagePlanResult.Value!.Diffs),
                skillDirectory,
                package,
                null))
            : SkillOperationResult<SkillUpdateActionPlan>.FailureResult(packagePlanResult.Failure!.Code, packagePlanResult.Failure.Message);
    }

    private async ValueTask<SkillOperationResult<bool>> ValidateWritePreconditionAsync (
        CanonicalSkillPackage package,
        string host,
        string skillDirectory,
        SkillUpdateActionKind actionKind,
        bool force,
        CancellationToken cancellationToken)
    {
        var stateResult = await targetStateAnalyzer.AnalyzeAsync(package, skillDirectory, host, cancellationToken).ConfigureAwait(false);
        if (!stateResult.IsSuccess)
        {
            return SkillOperationResult<bool>.FailureResult(stateResult.Failure!.Code, stateResult.Failure.Message);
        }

        var state = stateResult.Value!.Kind;
        var isValid = actionKind switch
        {
            SkillUpdateActionKind.Created => state == SkillInstalledTargetStateKind.Missing,
            SkillUpdateActionKind.Updated when force => state == SkillInstalledTargetStateKind.CleanOutdated || state == SkillInstalledTargetStateKind.LocalModified,
            SkillUpdateActionKind.Updated => state == SkillInstalledTargetStateKind.CleanOutdated,
            _ => true,
        };
        if (isValid)
        {
            return SkillOperationResult<bool>.Success(true);
        }

        return SkillOperationResult<bool>.FailureResult(
            ResolveChangedTargetFailureCode(state),
            $"Target skill directory changed after planning; refusing to write: {skillDirectory}");
    }

    private static SkillMaterializedPackageWriteMode ResolveWriteMode (SkillUpdateActionKind actionKind)
    {
        return actionKind switch
        {
            SkillUpdateActionKind.Created => SkillMaterializedPackageWriteMode.CreateNew,
            SkillUpdateActionKind.Updated => SkillMaterializedPackageWriteMode.ReplaceExisting,
            _ => throw new ArgumentOutOfRangeException(nameof(actionKind), actionKind, "Action does not write a materialized package."),
        };
    }

    private static SkillFailureCode ResolveChangedTargetFailureCode (SkillInstalledTargetStateKind state)
    {
        return state == SkillInstalledTargetStateKind.Unmanaged
            ? SkillFailureCodes.InstallTargetUnmanaged
            : SkillFailureCodes.InstallTargetDigestMismatch;
    }

    private async ValueTask<SkillOperationResult<SkillMaterializedPackagePlan>> CreateMaterializedPackagePlanAsync (
        CanonicalSkillPackage package,
        string host,
        string skillDirectory,
        bool printDiff,
        CancellationToken cancellationToken)
    {
        var materializedResult = materializationService.Materialize(package, host);
        if (!materializedResult.IsSuccess)
        {
            return SkillOperationResult<SkillMaterializedPackagePlan>.FailureResult(materializedResult.Failure!.Code, materializedResult.Failure.Message);
        }

        var diffResult = await diffBuilder.BuildOptionalAsync(skillDirectory, materializedResult.Value!, printDiff, cancellationToken).ConfigureAwait(false);
        return diffResult.IsSuccess
            ? SkillOperationResult<SkillMaterializedPackagePlan>.Success(new SkillMaterializedPackagePlan(materializedResult.Value!, diffResult.Value!))
            : SkillOperationResult<SkillMaterializedPackagePlan>.FailureResult(diffResult.Failure!.Code, diffResult.Failure.Message);
    }

    private sealed record SkillMaterializedPackagePlan (
        SkillMaterializedPackage MaterializedPackage,
        IReadOnlyList<SkillActionDiff> Diffs);

    private sealed record SkillUpdateActionPlan (
        SkillUpdateAction Action,
        string SkillDirectory,
        CanonicalSkillPackage Package,
        SkillMaterializedPackage? MaterializedPackage);
}
