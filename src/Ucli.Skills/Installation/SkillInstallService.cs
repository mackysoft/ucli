using MackySoft.Ucli.Skills.Installation.Validation;
using MackySoft.Ucli.Skills.Materialization;
using MackySoft.Ucli.Skills.Packaging;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Installs official SKILL packages into a host target root. </summary>
public sealed class SkillInstallService
{
    private readonly SkillInstallTargetResolver targetResolver;
    private readonly SkillMaterializationService materializationService;
    private readonly SkillInstalledPackageValidator installedPackageValidator;

    /// <summary> Initializes a new instance of the <see cref="SkillInstallService" /> class. </summary>
    /// <param name="targetResolver"> The target resolver. </param>
    /// <param name="materializationService"> The materialization service. </param>
    /// <param name="installedPackageValidator"> The installed package validator. </param>
    public SkillInstallService (
        SkillInstallTargetResolver targetResolver,
        SkillMaterializationService materializationService,
        SkillInstalledPackageValidator installedPackageValidator)
    {
        this.targetResolver = targetResolver ?? throw new ArgumentNullException(nameof(targetResolver));
        this.materializationService = materializationService ?? throw new ArgumentNullException(nameof(materializationService));
        this.installedPackageValidator = installedPackageValidator ?? throw new ArgumentNullException(nameof(installedPackageValidator));
    }

    /// <summary> Installs official SKILL packages. </summary>
    /// <param name="packages"> The canonical packages. </param>
    /// <param name="request"> The install request. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The install result or failure. </returns>
    public async ValueTask<SkillOperationResult<SkillInstallResult>> InstallAsync (
        IReadOnlyList<CanonicalSkillPackage> packages,
        SkillInstallRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packages);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var targetResult = targetResolver.ResolveTarget(request);
        if (!targetResult.IsSuccess)
        {
            return SkillOperationResult<SkillInstallResult>.FailureResult(targetResult.Failure!.Code, targetResult.Failure.Message);
        }

        var target = targetResult.Value!;
        var targetRoot = target.TargetRoot;
        var actions = new List<SkillInstallAction>();
        foreach (var package in packages.OrderBy(static package => package.Manifest.SkillName, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var skillDirectoryResult = SkillPackagePathBoundary.ResolvePackageDirectory(targetRoot, package.Manifest.SkillName);
            if (!skillDirectoryResult.IsSuccess)
            {
                return SkillOperationResult<SkillInstallResult>.FailureResult(skillDirectoryResult.Failure!.Code, skillDirectoryResult.Failure.Message);
            }

            var skillDirectory = skillDirectoryResult.Value!;
            var identity = new SkillInstallIdentity(target.Host, request.Scope, targetRoot, package.Manifest.SkillName);
            if (Directory.Exists(skillDirectory))
            {
                var existingResult = await ValidateExistingTargetAsync(package, skillDirectory, target.Host, cancellationToken).ConfigureAwait(false);
                if (!existingResult.IsSuccess)
                {
                    return SkillOperationResult<SkillInstallResult>.FailureResult(existingResult.Failure!.Code, existingResult.Failure.Message);
                }

                actions.Add(new SkillInstallAction(identity, SkillInstallActionKind.NoOp));
                continue;
            }

            var materializedResult = materializationService.Materialize(package, target.Host);
            if (!materializedResult.IsSuccess)
            {
                return SkillOperationResult<SkillInstallResult>.FailureResult(materializedResult.Failure!.Code, materializedResult.Failure.Message);
            }

            foreach (var file in materializedResult.Value!.Files)
            {
                var filePathResult = SkillPackagePathBoundary.ResolvePackageFilePathUnderRoot(targetRoot, skillDirectory, file.RelativePath);
                if (!filePathResult.IsSuccess)
                {
                    return SkillOperationResult<SkillInstallResult>.FailureResult(filePathResult.Failure!.Code, filePathResult.Failure.Message);
                }

                await SkillPackageFileWriter.WriteAllTextAtomically(filePathResult.Value!, file.Content, cancellationToken).ConfigureAwait(false);
            }

            actions.Add(new SkillInstallAction(identity, SkillInstallActionKind.Created));
        }

        return SkillOperationResult<SkillInstallResult>.Success(new SkillInstallResult(targetRoot, actions));
    }

    private async ValueTask<SkillOperationResult<bool>> ValidateExistingTargetAsync (
        CanonicalSkillPackage package,
        string skillDirectory,
        string host,
        CancellationToken cancellationToken)
    {
        var validationResult = await installedPackageValidator.ValidateAsync(package, skillDirectory, host, cancellationToken).ConfigureAwait(false);
        return validationResult.IsSuccess
            ? SkillOperationResult<bool>.Success(true)
            : SkillOperationResult<bool>.FailureResult(validationResult.Failure!.Code, validationResult.Failure.Message);
    }
}
