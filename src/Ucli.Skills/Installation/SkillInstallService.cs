using MackySoft.Ucli.Skills.Generation;
using MackySoft.Ucli.Skills.Manifests;
using MackySoft.Ucli.Skills.Materialization;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Installs official SKILL packages into a host target root. </summary>
public sealed class SkillInstallService
{
    private readonly SkillInstallTargetResolver targetResolver;
    private readonly SkillMaterializationService materializationService;
    private readonly SkillManifestJsonSerializer manifestSerializer;
    private readonly SkillManifestValidator manifestValidator;
    private readonly SkillHostMaterializationInspector hostInspector;
    private readonly SkillInstalledContentDigestVerifier contentDigestVerifier;

    /// <summary> Initializes a new instance of the <see cref="SkillInstallService" /> class. </summary>
    /// <param name="targetResolver"> The target resolver. </param>
    /// <param name="materializationService"> The materialization service. </param>
    /// <param name="manifestSerializer"> The manifest serializer. </param>
    /// <param name="manifestValidator"> The manifest validator. </param>
    /// <param name="hostInspector"> The host materialization inspector. </param>
    /// <param name="contentDigestVerifier"> The installed content digest verifier. </param>
    public SkillInstallService (
        SkillInstallTargetResolver? targetResolver = null,
        SkillMaterializationService? materializationService = null,
        SkillManifestJsonSerializer? manifestSerializer = null,
        SkillManifestValidator? manifestValidator = null,
        SkillHostMaterializationInspector? hostInspector = null,
        SkillInstalledContentDigestVerifier? contentDigestVerifier = null)
    {
        this.targetResolver = targetResolver ?? new SkillInstallTargetResolver();
        this.materializationService = materializationService ?? new SkillMaterializationService();
        this.manifestSerializer = manifestSerializer ?? new SkillManifestJsonSerializer();
        this.manifestValidator = manifestValidator ?? new SkillManifestValidator();
        this.hostInspector = hostInspector ?? new SkillHostMaterializationInspector();
        this.contentDigestVerifier = contentDigestVerifier ?? new SkillInstalledContentDigestVerifier();
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

        var targetRootResult = targetResolver.ResolveTargetRoot(request);
        if (!targetRootResult.IsSuccess)
        {
            return SkillOperationResult<SkillInstallResult>.FailureResult(targetRootResult.Failure!.Code, targetRootResult.Failure.Message);
        }

        var targetRoot = targetRootResult.Value!;
        var actions = new List<SkillInstallAction>();
        foreach (var package in packages.OrderBy(static package => package.SkillName, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var skillDirectoryResult = SkillPathBoundary.ResolvePackageDirectory(targetRoot, package.SkillName);
            if (!skillDirectoryResult.IsSuccess)
            {
                return SkillOperationResult<SkillInstallResult>.FailureResult(skillDirectoryResult.Failure!.Code, skillDirectoryResult.Failure.Message);
            }

            var skillDirectory = skillDirectoryResult.Value!;
            var identity = new SkillInstallIdentity(request.Host, request.Scope, targetRoot, package.SkillName);
            if (Directory.Exists(skillDirectory))
            {
                var existingResult = await ValidateExistingTargetAsync(package, skillDirectory, request, cancellationToken).ConfigureAwait(false);
                if (!existingResult.IsSuccess)
                {
                    return SkillOperationResult<SkillInstallResult>.FailureResult(existingResult.Failure!.Code, existingResult.Failure.Message);
                }

                actions.Add(new SkillInstallAction(identity, SkillInstallActionKind.NoOp));
                continue;
            }

            var materializedResult = materializationService.Materialize(package, request.Host);
            if (!materializedResult.IsSuccess)
            {
                return SkillOperationResult<SkillInstallResult>.FailureResult(materializedResult.Failure!.Code, materializedResult.Failure.Message);
            }

            foreach (var file in materializedResult.Value!.Files)
            {
                var filePathResult = SkillPathBoundary.ResolvePackageFilePathUnderRoot(targetRoot, skillDirectory, file.RelativePath);
                if (!filePathResult.IsSuccess)
                {
                    return SkillOperationResult<SkillInstallResult>.FailureResult(filePathResult.Failure!.Code, filePathResult.Failure.Message);
                }

                await SkillFileUtilities.WriteAllTextAtomically(filePathResult.Value!, file.Content, cancellationToken).ConfigureAwait(false);
            }

            actions.Add(new SkillInstallAction(identity, SkillInstallActionKind.Created));
        }

        return SkillOperationResult<SkillInstallResult>.Success(new SkillInstallResult(targetRoot, actions));
    }

    private async ValueTask<SkillOperationResult<bool>> ValidateExistingTargetAsync (
        CanonicalSkillPackage package,
        string skillDirectory,
        SkillInstallRequest request,
        CancellationToken cancellationToken)
    {
        var manifestPathResult = SkillPathBoundary.ResolvePackageFilePath(skillDirectory, "ucli-skill.json");
        if (!manifestPathResult.IsSuccess)
        {
            return SkillOperationResult<bool>.FailureResult(manifestPathResult.Failure!.Code, manifestPathResult.Failure.Message);
        }

        var manifestPath = manifestPathResult.Value!;
        if (!File.Exists(manifestPath))
        {
            return SkillOperationResult<bool>.FailureResult(
                SkillFailureCodes.InstallTargetUnmanaged,
                $"Target skill directory is missing ucli-skill.json: {skillDirectory}");
        }

        var manifestResult = manifestSerializer.TryDeserialize(await File.ReadAllTextAsync(manifestPath, cancellationToken).ConfigureAwait(false));
        if (!manifestResult.IsSuccess)
        {
            return SkillOperationResult<bool>.FailureResult(manifestResult.Failure!.Code, $"Target skill manifest is invalid: {manifestPath}");
        }

        var manifest = manifestResult.Value!;
        var validationResult = manifestValidator.Validate(manifest);
        if (!validationResult.IsSuccess)
        {
            return SkillOperationResult<bool>.FailureResult(validationResult.Failure!.Code, validationResult.Failure.Message);
        }

        if (!string.Equals(manifest.ContentDigest, package.Manifest.ContentDigest, StringComparison.Ordinal))
        {
            return SkillOperationResult<bool>.FailureResult(
                SkillFailureCodes.InstallTargetDigestMismatch,
                $"Target skill contentDigest does not match canonical package: {package.SkillName}");
        }

        var materializedResult = materializationService.Materialize(package, request.Host);
        if (!materializedResult.IsSuccess)
        {
            return SkillOperationResult<bool>.FailureResult(materializedResult.Failure!.Code, materializedResult.Failure.Message);
        }

        var installedDigestResult = await contentDigestVerifier.MatchesContentDigestAsync(
            skillDirectory,
            package,
            materializedResult.Value!.Files,
            cancellationToken).ConfigureAwait(false);
        if (!installedDigestResult.IsSuccess)
        {
            return SkillOperationResult<bool>.FailureResult(installedDigestResult.Failure!.Code, installedDigestResult.Failure.Message);
        }

        if (!installedDigestResult.Value)
        {
            return SkillOperationResult<bool>.FailureResult(
                SkillFailureCodes.InstallTargetDigestMismatch,
                $"Target skill files do not match canonical package contentDigest: {package.SkillName}");
        }

        var hostMatchResult = await hostInspector.MatchesHostAsync(skillDirectory, manifest, request.Host, cancellationToken).ConfigureAwait(false);
        if (!hostMatchResult.IsSuccess)
        {
            return SkillOperationResult<bool>.FailureResult(hostMatchResult.Failure!.Code, hostMatchResult.Failure.Message);
        }

        return hostMatchResult.Value
            ? SkillOperationResult<bool>.Success(true)
            : SkillOperationResult<bool>.FailureResult(
                SkillFailureCodes.InstallTargetHostConflict,
                $"Target skill directory is materialized for another host: {skillDirectory}");
    }
}
