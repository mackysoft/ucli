using MackySoft.Ucli.Skills.Generation;
using MackySoft.Ucli.Skills.Hosts;
using MackySoft.Ucli.Skills.Installation;
using MackySoft.Ucli.Skills.Manifests;
using MackySoft.Ucli.Skills.Materialization;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Doctor;

/// <summary> Diagnoses host-materialized SKILL package directories. </summary>
public sealed class SkillDoctorService
{
    private readonly SkillManifestJsonSerializer manifestSerializer;
    private readonly SkillManifestValidator manifestValidator;
    private readonly SkillHostMaterializationInspector hostInspector;
    private readonly SkillInstalledContentDigestVerifier contentDigestVerifier;
    private readonly SkillMaterializationService materializationService;

    /// <summary> Initializes a new instance of the <see cref="SkillDoctorService" /> class. </summary>
    /// <param name="manifestSerializer"> The manifest serializer. </param>
    /// <param name="manifestValidator"> The manifest validator. </param>
    /// <param name="hostInspector"> The host materialization inspector. </param>
    /// <param name="contentDigestVerifier"> The installed content digest verifier. </param>
    /// <param name="materializationService"> The host materialization service. </param>
    public SkillDoctorService (
        SkillManifestJsonSerializer? manifestSerializer = null,
        SkillManifestValidator? manifestValidator = null,
        SkillHostMaterializationInspector? hostInspector = null,
        SkillInstalledContentDigestVerifier? contentDigestVerifier = null,
        SkillMaterializationService? materializationService = null)
    {
        this.manifestSerializer = manifestSerializer ?? new SkillManifestJsonSerializer();
        this.manifestValidator = manifestValidator ?? new SkillManifestValidator();
        this.hostInspector = hostInspector ?? new SkillHostMaterializationInspector();
        this.contentDigestVerifier = contentDigestVerifier ?? new SkillInstalledContentDigestVerifier();
        this.materializationService = materializationService ?? new SkillMaterializationService();
    }

    /// <summary> Diagnoses one host target root against canonical packages. </summary>
    /// <param name="packages"> The canonical packages. </param>
    /// <param name="host"> The target host. </param>
    /// <param name="targetRoot"> The host target root. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The doctor result. </returns>
    public async ValueTask<SkillDoctorResult> DiagnoseAsync (
        IReadOnlyList<CanonicalSkillPackage> packages,
        SkillHostKind host,
        string targetRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packages);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetRoot);
        cancellationToken.ThrowIfCancellationRequested();

        var diagnostics = new List<SkillDoctorDiagnostic>();
        var fullTargetRoot = Path.GetFullPath(targetRoot);
        if (!SkillHostKindCodec.TryToValue(host, out _))
        {
            diagnostics.Add(new SkillDoctorDiagnostic(
                SkillDoctorSeverity.Error,
                SkillFailureCodes.HostUnsupported,
                $"Unsupported SKILL host: {host}"));
            return new SkillDoctorResult(host, fullTargetRoot, diagnostics);
        }

        if (!Directory.Exists(fullTargetRoot))
        {
            diagnostics.Add(new SkillDoctorDiagnostic(
                SkillDoctorSeverity.Error,
                SkillFailureCodes.InstallTargetUnmanaged,
                $"Target root does not exist: {fullTargetRoot}"));
            return new SkillDoctorResult(host, fullTargetRoot, diagnostics);
        }

        foreach (var package in packages.OrderBy(static package => package.SkillName, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await DiagnosePackageAsync(package, host, fullTargetRoot, diagnostics, cancellationToken).ConfigureAwait(false);
        }

        if (diagnostics.Count == 0)
        {
            diagnostics.Add(new SkillDoctorDiagnostic(
                SkillDoctorSeverity.Info,
                "SKILL_DOCTOR_OK",
                "All official SKILL packages are installed for the requested host."));
        }

        return new SkillDoctorResult(host, fullTargetRoot, diagnostics);
    }

    private async ValueTask DiagnosePackageAsync (
        CanonicalSkillPackage package,
        SkillHostKind host,
        string targetRoot,
        List<SkillDoctorDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var skillDirectoryResult = SkillPathBoundary.ResolvePackageDirectory(targetRoot, package.SkillName);
        if (!skillDirectoryResult.IsSuccess)
        {
            diagnostics.Add(Error(skillDirectoryResult.Failure!.Code, skillDirectoryResult.Failure.Message, package.SkillName));
            return;
        }

        var skillDirectory = skillDirectoryResult.Value!;
        if (!Directory.Exists(skillDirectory))
        {
            diagnostics.Add(Error(SkillFailureCodes.InstallTargetUnmanaged, "Skill directory is missing.", package.SkillName));
            return;
        }

        var manifestPathResult = SkillPathBoundary.ResolvePackageFilePath(skillDirectory, "ucli-skill.json");
        if (!manifestPathResult.IsSuccess)
        {
            diagnostics.Add(Error(manifestPathResult.Failure!.Code, manifestPathResult.Failure.Message, package.SkillName));
            return;
        }

        var manifestPath = manifestPathResult.Value!;
        if (!File.Exists(manifestPath))
        {
            diagnostics.Add(Error(SkillFailureCodes.InstallTargetUnmanaged, "ucli-skill.json is missing.", package.SkillName));
            return;
        }

        var manifestResult = manifestSerializer.TryDeserialize(await File.ReadAllTextAsync(manifestPath, cancellationToken).ConfigureAwait(false));
        if (!manifestResult.IsSuccess)
        {
            diagnostics.Add(Error(SkillFailureCodes.ManifestInvalid, "ucli-skill.json is invalid.", package.SkillName));
            return;
        }

        var manifest = manifestResult.Value!;
        var validationResult = manifestValidator.Validate(manifest);
        if (!validationResult.IsSuccess)
        {
            diagnostics.Add(Error(validationResult.Failure!.Code, validationResult.Failure.Message, package.SkillName));
            return;
        }

        if (!string.Equals(manifest.ContentDigest, package.Manifest.ContentDigest, StringComparison.Ordinal))
        {
            diagnostics.Add(Error(SkillFailureCodes.InstallTargetDigestMismatch, "contentDigest does not match canonical package.", package.SkillName));
        }

        var materializedResult = materializationService.Materialize(package, host);
        if (!materializedResult.IsSuccess)
        {
            diagnostics.Add(Error(materializedResult.Failure!.Code, materializedResult.Failure.Message, package.SkillName));
            return;
        }

        var installedDigestResult = await contentDigestVerifier.MatchesContentDigestAsync(
            skillDirectory,
            package,
            materializedResult.Value!.Files,
            cancellationToken).ConfigureAwait(false);
        if (!installedDigestResult.IsSuccess)
        {
            diagnostics.Add(Error(installedDigestResult.Failure!.Code, installedDigestResult.Failure.Message, package.SkillName));
            return;
        }

        if (!installedDigestResult.Value)
        {
            diagnostics.Add(Error(SkillFailureCodes.InstallTargetDigestMismatch, "Installed files do not match canonical contentDigest.", package.SkillName));
        }

        var hostMatchResult = await hostInspector.MatchesHostAsync(skillDirectory, manifest, host, cancellationToken).ConfigureAwait(false);
        if (!hostMatchResult.IsSuccess)
        {
            diagnostics.Add(Error(hostMatchResult.Failure!.Code, hostMatchResult.Failure.Message, package.SkillName));
            return;
        }

        if (!hostMatchResult.Value)
        {
            diagnostics.Add(Error(SkillFailureCodes.InstallTargetHostConflict, "Materialized host artifacts do not match the requested host.", package.SkillName));
        }
    }

    private static SkillDoctorDiagnostic Error (
        string code,
        string message,
        string skillName)
    {
        return new SkillDoctorDiagnostic(SkillDoctorSeverity.Error, code, message, skillName);
    }
}
