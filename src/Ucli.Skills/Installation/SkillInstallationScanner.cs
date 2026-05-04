using MackySoft.Ucli.Skills.Generation;
using MackySoft.Ucli.Skills.Hosts.Registration;
using MackySoft.Ucli.Skills.Manifests;
using MackySoft.Ucli.Skills.Materialization;
using MackySoft.Ucli.Skills.Packaging;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Scans installed SKILL manifests under one host target root. </summary>
public sealed class SkillInstallationScanner
{
    private readonly SkillHostAdapterSet hostAdapters;
    private readonly SkillManifestJsonSerializer manifestSerializer;
    private readonly SkillManifestValidator manifestValidator;
    private readonly SkillHostMaterializationInspector hostInspector;
    private readonly SkillInstalledContentDigestVerifier contentDigestVerifier;
    private readonly SkillMaterializationService materializationService;

    /// <summary> Initializes a new instance of the <see cref="SkillInstallationScanner" /> class. </summary>
    /// <param name="hostAdapters"> The supported host adapter set. </param>
    /// <param name="manifestSerializer"> The manifest serializer. </param>
    /// <param name="manifestValidator"> The manifest validator. </param>
    /// <param name="hostInspector"> The host materialization inspector. </param>
    /// <param name="contentDigestVerifier"> The installed content digest verifier. </param>
    /// <param name="materializationService"> The host materialization service. </param>
    public SkillInstallationScanner (
        SkillHostAdapterSet? hostAdapters = null,
        SkillManifestJsonSerializer? manifestSerializer = null,
        SkillManifestValidator? manifestValidator = null,
        SkillHostMaterializationInspector? hostInspector = null,
        SkillInstalledContentDigestVerifier? contentDigestVerifier = null,
        SkillMaterializationService? materializationService = null)
    {
        this.hostAdapters = hostAdapters ?? new SkillHostAdapterSet();
        this.manifestSerializer = manifestSerializer ?? new SkillManifestJsonSerializer();
        this.manifestValidator = manifestValidator ?? new SkillManifestValidator();
        this.hostInspector = hostInspector ?? new SkillHostMaterializationInspector();
        this.contentDigestVerifier = contentDigestVerifier ?? new SkillInstalledContentDigestVerifier();
        this.materializationService = materializationService ?? new SkillMaterializationService();
    }

    /// <summary> Scans installed SKILL manifests. </summary>
    /// <param name="packages"> The canonical packages used for digest verification. </param>
    /// <param name="targetRoot"> The host target root. </param>
    /// <param name="host"> The host used for install identity. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The installed skill list or manifest failure. </returns>
    public async ValueTask<SkillOperationResult<IReadOnlyList<SkillInstalledSkill>>> ScanAsync (
        IReadOnlyList<CanonicalSkillPackage> packages,
        string targetRoot,
        string host,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packages);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetRoot);
        cancellationToken.ThrowIfCancellationRequested();

        var adapterResult = hostAdapters.GetAdapter(host);
        if (!adapterResult.IsSuccess)
        {
            return SkillOperationResult<IReadOnlyList<SkillInstalledSkill>>.FailureResult(
                adapterResult.Failure!.Code,
                adapterResult.Failure.Message);
        }

        var hostKey = adapterResult.Value!.Descriptor.HostKey;
        var fullTargetRoot = Path.GetFullPath(targetRoot);
        if (!Directory.Exists(fullTargetRoot))
        {
            return SkillOperationResult<IReadOnlyList<SkillInstalledSkill>>.Success(Array.Empty<SkillInstalledSkill>());
        }

        var packageByName = packages.ToDictionary(static package => package.SkillName, StringComparer.Ordinal);
        var installedSkills = new List<SkillInstalledSkill>();
        foreach (var skillDirectory in Directory.EnumerateDirectories(fullTargetRoot).Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var skillDirectoryResult = SkillPackagePathBoundary.ResolveUnderRoot(fullTargetRoot, skillDirectory);
            if (!skillDirectoryResult.IsSuccess)
            {
                return SkillOperationResult<IReadOnlyList<SkillInstalledSkill>>.FailureResult(
                    skillDirectoryResult.Failure!.Code,
                    skillDirectoryResult.Failure.Message);
            }

            var resolvedSkillDirectory = skillDirectoryResult.Value!;
            var manifestPathResult = SkillPackagePathBoundary.ResolvePackageFilePathUnderRoot(fullTargetRoot, resolvedSkillDirectory, "ucli-skill.json");
            if (!manifestPathResult.IsSuccess)
            {
                return SkillOperationResult<IReadOnlyList<SkillInstalledSkill>>.FailureResult(
                    manifestPathResult.Failure!.Code,
                    manifestPathResult.Failure.Message);
            }

            if (!File.Exists(manifestPathResult.Value!))
            {
                continue;
            }

            var manifestText = await File.ReadAllTextAsync(manifestPathResult.Value!, cancellationToken).ConfigureAwait(false);
            var manifestResult = manifestSerializer.TryDeserialize(manifestText);
            if (!manifestResult.IsSuccess)
            {
                return SkillOperationResult<IReadOnlyList<SkillInstalledSkill>>.FailureResult(
                    manifestResult.Failure!.Code,
                    $"Invalid ucli-skill.json: {manifestPathResult.Value!}");
            }

            var manifest = manifestResult.Value!;
            var validationResult = manifestValidator.Validate(manifest);
            if (!validationResult.IsSuccess)
            {
                return SkillOperationResult<IReadOnlyList<SkillInstalledSkill>>.FailureResult(
                    validationResult.Failure!.Code,
                    validationResult.Failure.Message);
            }

            if (!string.Equals(Path.GetFileName(resolvedSkillDirectory), manifest.SkillName, StringComparison.Ordinal))
            {
                return SkillOperationResult<IReadOnlyList<SkillInstalledSkill>>.FailureResult(
                    SkillFailureCodes.ManifestInvalid,
                    $"ucli-skill.json skillName must match installed directory name: {manifestPathResult.Value!}");
            }

            if (!packageByName.TryGetValue(manifest.SkillName, out var package))
            {
                return SkillOperationResult<IReadOnlyList<SkillInstalledSkill>>.FailureResult(
                    SkillFailureCodes.InstallTargetUnmanaged,
                    $"Installed SKILL is not part of the canonical package set: {manifest.SkillName}");
            }

            if (!string.Equals(manifest.ContentDigest, package.Manifest.ContentDigest, StringComparison.Ordinal))
            {
                return SkillOperationResult<IReadOnlyList<SkillInstalledSkill>>.FailureResult(
                    SkillFailureCodes.InstallTargetDigestMismatch,
                    $"Installed SKILL contentDigest does not match canonical package: {manifest.SkillName}");
            }

            var materializedResult = materializationService.Materialize(package, hostKey);
            if (!materializedResult.IsSuccess)
            {
                return SkillOperationResult<IReadOnlyList<SkillInstalledSkill>>.FailureResult(
                    materializedResult.Failure!.Code,
                    materializedResult.Failure.Message);
            }

            var digestResult = await contentDigestVerifier.MatchesContentDigestAsync(
                resolvedSkillDirectory,
                package,
                materializedResult.Value!.Files,
                cancellationToken).ConfigureAwait(false);
            if (!digestResult.IsSuccess)
            {
                return SkillOperationResult<IReadOnlyList<SkillInstalledSkill>>.FailureResult(digestResult.Failure!.Code, digestResult.Failure.Message);
            }

            if (!digestResult.Value)
            {
                return SkillOperationResult<IReadOnlyList<SkillInstalledSkill>>.FailureResult(
                    SkillFailureCodes.InstallTargetDigestMismatch,
                    $"Installed SKILL files do not match canonical package: {manifest.SkillName}");
            }

            var hostMatchResult = await hostInspector.MatchesHostAsync(resolvedSkillDirectory, manifest, hostKey, cancellationToken).ConfigureAwait(false);
            if (!hostMatchResult.IsSuccess)
            {
                return SkillOperationResult<IReadOnlyList<SkillInstalledSkill>>.FailureResult(hostMatchResult.Failure!.Code, hostMatchResult.Failure.Message);
            }

            if (!hostMatchResult.Value)
            {
                return SkillOperationResult<IReadOnlyList<SkillInstalledSkill>>.FailureResult(
                    SkillFailureCodes.InstallTargetHostConflict,
                    $"Installed skill directory is materialized for another host: {resolvedSkillDirectory}");
            }

            installedSkills.Add(new SkillInstalledSkill(
                new SkillInstallIdentity(hostKey, SkillScopeKind.Project, fullTargetRoot, manifest.SkillName),
                resolvedSkillDirectory,
                manifest));
        }

        return SkillOperationResult<IReadOnlyList<SkillInstalledSkill>>.Success(installedSkills);
    }
}
