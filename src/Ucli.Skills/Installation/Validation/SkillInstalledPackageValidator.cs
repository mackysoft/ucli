using MackySoft.Ucli.Skills.Generation;
using MackySoft.Ucli.Skills.Hosts.Registration;
using MackySoft.Ucli.Skills.Manifests;
using MackySoft.Ucli.Skills.Materialization;
using MackySoft.Ucli.Skills.Packaging;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Installation.Validation;

/// <summary> Validates an installed SKILL directory against one canonical package. </summary>
public sealed class SkillInstalledPackageValidator
{
    private readonly SkillInstalledManifestReader installedManifestReader;
    private readonly SkillMaterializationService materializationService;
    private readonly SkillInstalledContentDigestVerifier contentDigestVerifier;
    private readonly SkillInstalledFileSetVerifier fileSetVerifier;
    private readonly SkillHostMaterializationInspector hostInspector;

    /// <summary> Initializes a new instance of the <see cref="SkillInstalledPackageValidator" /> class. </summary>
    /// <param name="hostAdapters"> The supported host adapter set. </param>
    /// <param name="installedManifestReader"> The installed manifest reader. </param>
    /// <param name="materializationService"> The materialization service. </param>
    /// <param name="contentDigestVerifier"> The installed content digest verifier. </param>
    /// <param name="fileSetVerifier"> The installed materialized file-set verifier. </param>
    /// <param name="hostInspector"> The host materialization inspector. </param>
    public SkillInstalledPackageValidator (
        SkillHostAdapterSet hostAdapters,
        SkillInstalledManifestReader? installedManifestReader = null,
        SkillMaterializationService? materializationService = null,
        SkillInstalledContentDigestVerifier? contentDigestVerifier = null,
        SkillInstalledFileSetVerifier? fileSetVerifier = null,
        SkillHostMaterializationInspector? hostInspector = null)
    {
        ArgumentNullException.ThrowIfNull(hostAdapters);

        this.installedManifestReader = installedManifestReader ?? new SkillInstalledManifestReader(hostAdapters);
        this.materializationService = materializationService ?? new SkillMaterializationService(hostAdapters);
        this.contentDigestVerifier = contentDigestVerifier ?? new SkillInstalledContentDigestVerifier();
        this.fileSetVerifier = fileSetVerifier ?? new SkillInstalledFileSetVerifier();
        this.hostInspector = hostInspector ?? new SkillHostMaterializationInspector(hostAdapters);
    }

    /// <summary> Validates one installed package directory. </summary>
    /// <param name="package"> The canonical package. </param>
    /// <param name="skillDirectory"> The installed skill directory. </param>
    /// <param name="host"> The requested host. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The installed manifest when validation succeeds; otherwise a validation failure. </returns>
    public async ValueTask<SkillOperationResult<SkillManifest>> ValidateAsync (
        CanonicalSkillPackage package,
        string skillDirectory,
        string host,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentException.ThrowIfNullOrWhiteSpace(skillDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        cancellationToken.ThrowIfCancellationRequested();

        var installedManifestResult = await installedManifestReader.ReadRequiredAsync(skillDirectory, cancellationToken).ConfigureAwait(false);
        if (!installedManifestResult.IsSuccess)
        {
            return SkillOperationResult<SkillManifest>.FailureResult(
                installedManifestResult.Failure!.Code,
                installedManifestResult.Failure.Message);
        }

        var installedManifest = installedManifestResult.Value!;
        var manifest = installedManifest.Manifest;
        if (!string.Equals(manifest.ContentDigest, package.Manifest.ContentDigest, StringComparison.Ordinal))
        {
            return SkillOperationResult<SkillManifest>.FailureResult(
                SkillFailureCodes.InstallTargetDigestMismatch,
                $"Installed SKILL contentDigest does not match canonical package: {package.SkillName}");
        }

        var canonicalManifestText = package.Files.Single(static file => file.RelativePath == "ucli-skill.json").Content;
        if (!string.Equals(installedManifest.ManifestText, canonicalManifestText, StringComparison.Ordinal))
        {
            return SkillOperationResult<SkillManifest>.FailureResult(
                SkillFailureCodes.ManifestInvalid,
                $"ucli-skill.json does not match the canonical manifest: {package.SkillName}");
        }

        var materializedResult = materializationService.Materialize(package, host);
        if (!materializedResult.IsSuccess)
        {
            return SkillOperationResult<SkillManifest>.FailureResult(materializedResult.Failure!.Code, materializedResult.Failure.Message);
        }

        var differentHostResult = await hostInspector.MatchesDifferentHostAsync(skillDirectory, package.Manifest, host, cancellationToken).ConfigureAwait(false);
        if (!differentHostResult.IsSuccess)
        {
            return SkillOperationResult<SkillManifest>.FailureResult(differentHostResult.Failure!.Code, differentHostResult.Failure.Message);
        }

        if (differentHostResult.Value)
        {
            return SkillOperationResult<SkillManifest>.FailureResult(
                SkillFailureCodes.InstallTargetHostConflict,
                $"Installed skill directory is materialized for another host: {skillDirectory}");
        }

        var installedDigestResult = await contentDigestVerifier.MatchesContentDigestAsync(
            skillDirectory,
            package,
            cancellationToken).ConfigureAwait(false);
        if (!installedDigestResult.IsSuccess)
        {
            return SkillOperationResult<SkillManifest>.FailureResult(installedDigestResult.Failure!.Code, installedDigestResult.Failure.Message);
        }

        if (!installedDigestResult.Value)
        {
            return SkillOperationResult<SkillManifest>.FailureResult(
                SkillFailureCodes.InstallTargetDigestMismatch,
                $"Installed SKILL files do not match canonical package contentDigest: {package.SkillName}");
        }

        var hostMatchResult = await hostInspector.MatchesHostAsync(skillDirectory, package.Manifest, host, cancellationToken).ConfigureAwait(false);
        if (!hostMatchResult.IsSuccess)
        {
            return SkillOperationResult<SkillManifest>.FailureResult(hostMatchResult.Failure!.Code, hostMatchResult.Failure.Message);
        }

        if (!hostMatchResult.Value)
        {
            return SkillOperationResult<SkillManifest>.FailureResult(
                SkillFailureCodes.InstallTargetHostConflict,
                $"Installed skill directory is not materialized for requested host: {skillDirectory}");
        }

        var fileSetResult = fileSetVerifier.MatchesExpectedFiles(skillDirectory, materializedResult.Value!.Files);
        if (!fileSetResult.IsSuccess)
        {
            return SkillOperationResult<SkillManifest>.FailureResult(fileSetResult.Failure!.Code, fileSetResult.Failure.Message);
        }

        return fileSetResult.Value
            ? SkillOperationResult<SkillManifest>.Success(manifest)
            : SkillOperationResult<SkillManifest>.FailureResult(
                SkillFailureCodes.InstallTargetDigestMismatch,
                $"Installed SKILL file set does not match materialized package: {package.SkillName}");
    }
}
