using MackySoft.Ucli.Skills.Hosts.Registration;
using MackySoft.Ucli.Skills.Installation.Validation;
using MackySoft.Ucli.Skills.Packaging;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Scans installed SKILL manifests under one host target root. </summary>
public sealed class SkillInstallationScanner
{
    private readonly SkillHostAdapterSet hostAdapters;
    private readonly SkillInstalledManifestReader installedManifestReader;
    private readonly SkillInstalledPackageValidator installedPackageValidator;

    /// <summary> Initializes a new instance of the <see cref="SkillInstallationScanner" /> class. </summary>
    /// <param name="hostAdapters"> The supported host adapter set. </param>
    /// <param name="installedManifestReader"> The installed manifest reader. </param>
    /// <param name="installedPackageValidator"> The installed package validator. </param>
    public SkillInstallationScanner (
        SkillHostAdapterSet hostAdapters,
        SkillInstalledManifestReader installedManifestReader,
        SkillInstalledPackageValidator installedPackageValidator)
    {
        this.hostAdapters = hostAdapters ?? throw new ArgumentNullException(nameof(hostAdapters));
        this.installedManifestReader = installedManifestReader ?? throw new ArgumentNullException(nameof(installedManifestReader));
        this.installedPackageValidator = installedPackageValidator ?? throw new ArgumentNullException(nameof(installedPackageValidator));
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

        var packageByName = packages.ToDictionary(static package => package.Manifest.SkillName, StringComparer.Ordinal);
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

            var installedManifestResult = await installedManifestReader.ReadRequiredAsync(resolvedSkillDirectory, cancellationToken).ConfigureAwait(false);
            if (!installedManifestResult.IsSuccess)
            {
                return SkillOperationResult<IReadOnlyList<SkillInstalledSkill>>.FailureResult(
                    installedManifestResult.Failure!.Code,
                    installedManifestResult.Failure.Message);
            }

            var manifest = installedManifestResult.Value!.Manifest;
            if (!packageByName.TryGetValue(manifest.SkillName, out var package))
            {
                return SkillOperationResult<IReadOnlyList<SkillInstalledSkill>>.FailureResult(
                    SkillFailureCodes.InstallTargetUnmanaged,
                    $"Installed SKILL is not part of the canonical package set: {manifest.SkillName}");
            }

            var validationResult = await installedPackageValidator.ValidateAsync(package, resolvedSkillDirectory, hostKey, cancellationToken).ConfigureAwait(false);
            if (!validationResult.IsSuccess)
            {
                return SkillOperationResult<IReadOnlyList<SkillInstalledSkill>>.FailureResult(
                    validationResult.Failure!.Code,
                    validationResult.Failure.Message);
            }

            installedSkills.Add(new SkillInstalledSkill(
                new SkillInstallIdentity(hostKey, SkillScopeKind.Project, fullTargetRoot, manifest.SkillName),
                resolvedSkillDirectory,
                validationResult.Value!));
        }

        return SkillOperationResult<IReadOnlyList<SkillInstalledSkill>>.Success(installedSkills);
    }
}
