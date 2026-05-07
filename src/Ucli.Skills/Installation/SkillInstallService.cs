using MackySoft.Ucli.Skills.Materialization;
using MackySoft.Ucli.Skills.Packaging;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Installs official SKILL packages into a host target root. </summary>
public sealed class SkillInstallService
{
    private readonly SkillInstallTargetResolver targetResolver;
    private readonly SkillMaterializationService materializationService;
    private readonly SkillInstalledTargetStateAnalyzer targetStateAnalyzer;
    private readonly ISkillMaterializedPackageWriter packageWriter;
    private readonly SkillMaterializedPackageDiffBuilder diffBuilder;

    /// <summary> Initializes a new instance of the <see cref="SkillInstallService" /> class. </summary>
    /// <param name="targetResolver"> The target resolver. </param>
    /// <param name="materializationService"> The materialization service. </param>
    /// <param name="targetStateAnalyzer"> The installed target state analyzer. </param>
    /// <param name="packageWriter"> The materialized package writer. </param>
    /// <param name="diffBuilder"> The structured diff builder. </param>
    public SkillInstallService (
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

    /// <summary> Installs official SKILL packages. </summary>
    /// <param name="packages"> The canonical packages. </param>
    /// <param name="request"> The install request. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The install result or failure. </returns>
    public ValueTask<SkillOperationResult<SkillInstallResult>> InstallAsync (
        IReadOnlyList<CanonicalSkillPackage> packages,
        SkillInstallRequest request,
        CancellationToken cancellationToken = default)
    {
        return InstallAsync(new SkillInstallInput(packages, request), cancellationToken);
    }

    /// <summary> Installs official SKILL packages. </summary>
    /// <param name="input"> The install input. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The install result or failure. </returns>
    public async ValueTask<SkillOperationResult<SkillInstallResult>> InstallAsync (
        SkillInstallInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Packages);
        ArgumentNullException.ThrowIfNull(input.TargetRequest);
        cancellationToken.ThrowIfCancellationRequested();

        var targetResult = targetResolver.ResolveTarget(input.TargetRequest);
        if (!targetResult.IsSuccess)
        {
            return SkillOperationResult<SkillInstallResult>.FailureResult(targetResult.Failure!.Code, targetResult.Failure.Message);
        }

        var target = targetResult.Value!;
        var targetRoot = target.TargetRoot;
        var actionPlans = new List<SkillInstallActionPlan>();
        foreach (var package in input.Packages.OrderBy(static package => package.Manifest.SkillName, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var skillDirectoryResult = SkillPackagePathBoundary.ResolvePackageDirectory(targetRoot, package.Manifest.SkillName);
            if (!skillDirectoryResult.IsSuccess)
            {
                return SkillOperationResult<SkillInstallResult>.FailureResult(skillDirectoryResult.Failure!.Code, skillDirectoryResult.Failure.Message);
            }

            var skillDirectory = skillDirectoryResult.Value!;
            var identity = new SkillInstallIdentity(target.Host, input.TargetRequest.Scope, targetRoot, package.Manifest.SkillName);
            var stateResult = await targetStateAnalyzer.AnalyzeAsync(package, skillDirectory, target.Host, cancellationToken).ConfigureAwait(false);
            if (!stateResult.IsSuccess)
            {
                return SkillOperationResult<SkillInstallResult>.FailureResult(stateResult.Failure!.Code, stateResult.Failure.Message);
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
                return SkillOperationResult<SkillInstallResult>.FailureResult(actionPlanResult.Failure!.Code, actionPlanResult.Failure.Message);
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

                var writeResult = await packageWriter.WriteAsync(
                        targetRoot,
                        actionPlan.SkillDirectory,
                        actionPlan.MaterializedPackage,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (!writeResult.IsSuccess)
                {
                    return SkillOperationResult<SkillInstallResult>.FailureResult(writeResult.Failure!.Code, writeResult.Failure.Message);
                }
            }
        }

        return SkillOperationResult<SkillInstallResult>.Success(new SkillInstallResult(
            targetRoot,
            actionPlans.Select(static actionPlan => actionPlan.Action).ToArray(),
            input.DryRun,
            input.Force,
            input.PrintDiff));
    }

    private async ValueTask<SkillOperationResult<SkillInstallActionPlan>> CreateActionPlanAsync (
        CanonicalSkillPackage package,
        string host,
        string skillDirectory,
        SkillInstallIdentity identity,
        SkillInstalledTargetState state,
        SkillInstallInput input,
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
                        SkillInstallActionKind.Created,
                        input,
                        cancellationToken)
                    .ConfigureAwait(false);
            case SkillInstalledTargetStateKind.Current:
                return SkillOperationResult<SkillInstallActionPlan>.Success(new SkillInstallActionPlan(
                    new SkillInstallAction(identity, SkillInstallActionKind.NoOp),
                    skillDirectory,
                    null));
            case SkillInstalledTargetStateKind.CleanOutdated:
                return await CreateManagedMismatchActionPlanAsync(
                        package,
                        host,
                        skillDirectory,
                        identity,
                        input,
                        SkillInstallActionKind.BlockedManagedOverwrite,
                        SkillBlockedReason.ManagedOverwriteRequiresForce,
                        cancellationToken)
                    .ConfigureAwait(false);
            case SkillInstalledTargetStateKind.LocalModified:
                return await CreateManagedMismatchActionPlanAsync(
                        package,
                        host,
                        skillDirectory,
                        identity,
                        input,
                        SkillInstallActionKind.BlockedLocalModification,
                        SkillBlockedReason.LocalModificationRequiresForce,
                        cancellationToken)
                    .ConfigureAwait(false);
            case SkillInstalledTargetStateKind.Unmanaged:
                if (!input.DryRun)
                {
                    return SkillOperationResult<SkillInstallActionPlan>.FailureResult(
                        SkillFailureCodes.InstallTargetUnmanaged,
                        $"Target skill directory is not managed by uCLI: {skillDirectory}");
                }

                return await CreateBlockedActionPlanAsync(
                        package,
                        host,
                        skillDirectory,
                        identity,
                        SkillInstallActionKind.BlockedUnmanaged,
                        SkillBlockedReason.UnmanagedTarget,
                        printDiff: false,
                        cancellationToken)
                    .ConfigureAwait(false);
            default:
                throw new ArgumentOutOfRangeException(nameof(state), state.Kind, "Unsupported target state.");
        }
    }

    private async ValueTask<SkillOperationResult<SkillInstallActionPlan>> CreateManagedMismatchActionPlanAsync (
        CanonicalSkillPackage package,
        string host,
        string skillDirectory,
        SkillInstallIdentity identity,
        SkillInstallInput input,
        SkillInstallActionKind blockedActionKind,
        SkillBlockedReason blockedReason,
        CancellationToken cancellationToken)
    {
        if (input.Force)
        {
            return await CreateWriteActionPlanAsync(
                    package,
                    host,
                    skillDirectory,
                    identity,
                    SkillInstallActionKind.Updated,
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
                    blockedActionKind,
                    blockedReason,
                    input.PrintDiff,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return SkillOperationResult<SkillInstallActionPlan>.FailureResult(
            SkillFailureCodes.InstallTargetDigestMismatch,
            $"Target skill directory differs from the canonical package. Use --force to overwrite: {skillDirectory}");
    }

    private async ValueTask<SkillOperationResult<SkillInstallActionPlan>> CreateWriteActionPlanAsync (
        CanonicalSkillPackage package,
        string host,
        string skillDirectory,
        SkillInstallIdentity identity,
        SkillInstallActionKind actionKind,
        SkillInstallInput input,
        CancellationToken cancellationToken)
    {
        var packagePlanResult = await CreateMaterializedPackagePlanAsync(package, host, skillDirectory, input.PrintDiff, cancellationToken).ConfigureAwait(false);
        if (!packagePlanResult.IsSuccess)
        {
            return SkillOperationResult<SkillInstallActionPlan>.FailureResult(packagePlanResult.Failure!.Code, packagePlanResult.Failure.Message);
        }

        var packagePlan = packagePlanResult.Value!;
        return SkillOperationResult<SkillInstallActionPlan>.Success(new SkillInstallActionPlan(
            new SkillInstallAction(
                identity,
                actionKind,
                Diffs: packagePlan.Diffs),
            skillDirectory,
            packagePlan.MaterializedPackage));
    }

    private async ValueTask<SkillOperationResult<SkillInstallActionPlan>> CreateBlockedActionPlanAsync (
        CanonicalSkillPackage package,
        string host,
        string skillDirectory,
        SkillInstallIdentity identity,
        SkillInstallActionKind actionKind,
        SkillBlockedReason blockedReason,
        bool printDiff,
        CancellationToken cancellationToken)
    {
        var packagePlanResult = await CreateMaterializedPackagePlanAsync(package, host, skillDirectory, printDiff, cancellationToken).ConfigureAwait(false);
        return packagePlanResult.IsSuccess
            ? SkillOperationResult<SkillInstallActionPlan>.Success(new SkillInstallActionPlan(
                new SkillInstallAction(identity, actionKind, blockedReason, packagePlanResult.Value!.Diffs),
                skillDirectory,
                null))
            : SkillOperationResult<SkillInstallActionPlan>.FailureResult(packagePlanResult.Failure!.Code, packagePlanResult.Failure.Message);
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

    private sealed record SkillInstallActionPlan (
        SkillInstallAction Action,
        string SkillDirectory,
        SkillMaterializedPackage? MaterializedPackage);
}
