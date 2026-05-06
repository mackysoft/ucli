using MackySoft.Ucli.Skills.Installation.Validation;
using MackySoft.Ucli.Skills.Packaging;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Uninstalls official SKILL packages from a host target root. </summary>
public sealed class SkillUninstallService
{
    private readonly SkillInstallTargetResolver targetResolver;
    private readonly SkillInstalledPackageIntegrityVerifier installedPackageIntegrityVerifier;

    /// <summary> Initializes a new instance of the <see cref="SkillUninstallService" /> class. </summary>
    /// <param name="targetResolver"> The target resolver. </param>
    /// <param name="installedPackageIntegrityVerifier"> The installed package integrity verifier. </param>
    public SkillUninstallService (
        SkillInstallTargetResolver targetResolver,
        SkillInstalledPackageIntegrityVerifier installedPackageIntegrityVerifier)
    {
        this.targetResolver = targetResolver ?? throw new ArgumentNullException(nameof(targetResolver));
        this.installedPackageIntegrityVerifier = installedPackageIntegrityVerifier ?? throw new ArgumentNullException(nameof(installedPackageIntegrityVerifier));
    }

    /// <summary> Uninstalls official SKILL packages. </summary>
    /// <param name="packages"> The canonical packages. </param>
    /// <param name="request"> The uninstall request. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The uninstall result or failure. </returns>
    public async ValueTask<SkillOperationResult<SkillUninstallResult>> UninstallAsync (
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
            return SkillOperationResult<SkillUninstallResult>.FailureResult(targetResult.Failure!.Code, targetResult.Failure.Message);
        }

        var target = targetResult.Value!;
        var targetRoot = target.TargetRoot;
        var actions = new List<SkillUninstallAction>();
        foreach (var package in packages.OrderBy(static package => package.SkillName, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var skillDirectoryResult = SkillPackagePathBoundary.ResolvePackageDirectory(targetRoot, package.SkillName);
            if (!skillDirectoryResult.IsSuccess)
            {
                return SkillOperationResult<SkillUninstallResult>.FailureResult(skillDirectoryResult.Failure!.Code, skillDirectoryResult.Failure.Message);
            }

            var skillDirectory = skillDirectoryResult.Value!;
            var identity = new SkillInstallIdentity(target.Host, request.Scope, targetRoot, package.SkillName);
            if (!Directory.Exists(skillDirectory))
            {
                actions.Add(new SkillUninstallAction(identity, SkillUninstallActionKind.NoOp));
                continue;
            }

            var manifestPathResult = SkillPackagePathBoundary.ResolvePackageFilePathUnderRoot(targetRoot, skillDirectory, "ucli-skill.json");
            if (!manifestPathResult.IsSuccess)
            {
                return SkillOperationResult<SkillUninstallResult>.FailureResult(manifestPathResult.Failure!.Code, manifestPathResult.Failure.Message);
            }

            if (!File.Exists(manifestPathResult.Value!))
            {
                actions.Add(new SkillUninstallAction(identity, SkillUninstallActionKind.SkippedUnmanaged));
                continue;
            }

            var integrityResult = await installedPackageIntegrityVerifier.VerifyAsync(skillDirectory, target.Host, cancellationToken).ConfigureAwait(false);
            if (!integrityResult.IsSuccess)
            {
                return SkillOperationResult<SkillUninstallResult>.FailureResult(integrityResult.Failure!.Code, integrityResult.Failure.Message);
            }

            Directory.Delete(skillDirectory, recursive: true);
            actions.Add(new SkillUninstallAction(identity, SkillUninstallActionKind.Deleted));
        }

        return SkillOperationResult<SkillUninstallResult>.Success(new SkillUninstallResult(targetRoot, actions));
    }
}
