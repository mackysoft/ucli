using MackySoft.Ucli.Skills.Installation.Validation;
using MackySoft.Ucli.Skills.Materialization;
using MackySoft.Ucli.Skills.Packaging;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Updates official SKILL packages under a host target root. </summary>
public sealed class SkillUpdateService
{
    private readonly SkillInstallTargetResolver targetResolver;
    private readonly SkillMaterializationService materializationService;
    private readonly SkillInstalledPackageValidator installedPackageValidator;
    private readonly SkillInstalledPackageIntegrityVerifier installedPackageIntegrityVerifier;

    /// <summary> Initializes a new instance of the <see cref="SkillUpdateService" /> class. </summary>
    /// <param name="targetResolver"> The target resolver. </param>
    /// <param name="materializationService"> The materialization service. </param>
    /// <param name="installedPackageValidator"> The current installed package validator. </param>
    /// <param name="installedPackageIntegrityVerifier"> The installed package integrity verifier. </param>
    public SkillUpdateService (
        SkillInstallTargetResolver targetResolver,
        SkillMaterializationService materializationService,
        SkillInstalledPackageValidator installedPackageValidator,
        SkillInstalledPackageIntegrityVerifier installedPackageIntegrityVerifier)
    {
        this.targetResolver = targetResolver ?? throw new ArgumentNullException(nameof(targetResolver));
        this.materializationService = materializationService ?? throw new ArgumentNullException(nameof(materializationService));
        this.installedPackageValidator = installedPackageValidator ?? throw new ArgumentNullException(nameof(installedPackageValidator));
        this.installedPackageIntegrityVerifier = installedPackageIntegrityVerifier ?? throw new ArgumentNullException(nameof(installedPackageIntegrityVerifier));
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
        var actions = new List<SkillUpdateAction>();
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
            if (!Directory.Exists(skillDirectory))
            {
                var createResult = await WritePackageAsync(package, target.Host, targetRoot, skillDirectory, cancellationToken).ConfigureAwait(false);
                if (!createResult.IsSuccess)
                {
                    return SkillOperationResult<SkillUpdateResult>.FailureResult(createResult.Failure!.Code, createResult.Failure.Message);
                }

                actions.Add(new SkillUpdateAction(identity, SkillUpdateActionKind.Created));
                continue;
            }

            var currentResult = await installedPackageValidator.ValidateAsync(package, skillDirectory, target.Host, cancellationToken).ConfigureAwait(false);
            if (currentResult.IsSuccess)
            {
                actions.Add(new SkillUpdateAction(identity, SkillUpdateActionKind.NoOp));
                continue;
            }

            var integrityResult = await installedPackageIntegrityVerifier.VerifyAsync(skillDirectory, target.Host, cancellationToken).ConfigureAwait(false);
            if (!integrityResult.IsSuccess)
            {
                return SkillOperationResult<SkillUpdateResult>.FailureResult(integrityResult.Failure!.Code, integrityResult.Failure.Message);
            }

            Directory.Delete(skillDirectory, recursive: true);
            var updateResult = await WritePackageAsync(package, target.Host, targetRoot, skillDirectory, cancellationToken).ConfigureAwait(false);
            if (!updateResult.IsSuccess)
            {
                return SkillOperationResult<SkillUpdateResult>.FailureResult(updateResult.Failure!.Code, updateResult.Failure.Message);
            }

            actions.Add(new SkillUpdateAction(identity, SkillUpdateActionKind.Updated));
        }

        return SkillOperationResult<SkillUpdateResult>.Success(new SkillUpdateResult(targetRoot, actions));
    }

    private async ValueTask<SkillOperationResult<bool>> WritePackageAsync (
        CanonicalSkillPackage package,
        string host,
        string targetRoot,
        string skillDirectory,
        CancellationToken cancellationToken)
    {
        var materializedResult = materializationService.Materialize(package, host);
        if (!materializedResult.IsSuccess)
        {
            return SkillOperationResult<bool>.FailureResult(materializedResult.Failure!.Code, materializedResult.Failure.Message);
        }

        return await SkillMaterializedPackageWriter.WriteAsync(
                targetRoot,
                skillDirectory,
                materializedResult.Value!,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
