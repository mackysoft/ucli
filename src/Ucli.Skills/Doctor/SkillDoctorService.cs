using MackySoft.Ucli.Skills.Generation;
using MackySoft.Ucli.Skills.Hosts.Registration;
using MackySoft.Ucli.Skills.Installation.Validation;
using MackySoft.Ucli.Skills.Packaging;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Doctor;

/// <summary> Diagnoses host-materialized SKILL package directories. </summary>
public sealed class SkillDoctorService
{
    private readonly SkillHostAdapterSet hostAdapters;
    private readonly SkillInstalledPackageValidator installedPackageValidator;

    /// <summary> Initializes a new instance of the <see cref="SkillDoctorService" /> class. </summary>
    /// <param name="hostAdapters"> The supported host adapter set. </param>
    /// <param name="installedPackageValidator"> The installed package validator. </param>
    public SkillDoctorService (
        SkillHostAdapterSet hostAdapters,
        SkillInstalledPackageValidator? installedPackageValidator = null)
    {
        this.hostAdapters = hostAdapters ?? throw new ArgumentNullException(nameof(hostAdapters));
        this.installedPackageValidator = installedPackageValidator ?? new SkillInstalledPackageValidator(hostAdapters);
    }

    /// <summary> Diagnoses one host target root against canonical packages. </summary>
    /// <param name="packages"> The canonical packages. </param>
    /// <param name="host"> The target host. </param>
    /// <param name="targetRoot"> The host target root. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The doctor result. </returns>
    public async ValueTask<SkillDoctorResult> DiagnoseAsync (
        IReadOnlyList<CanonicalSkillPackage> packages,
        string host,
        string targetRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packages);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetRoot);
        cancellationToken.ThrowIfCancellationRequested();

        var diagnostics = new List<SkillDoctorDiagnostic>();
        var fullTargetRoot = Path.GetFullPath(targetRoot);
        var adapterResult = hostAdapters.GetAdapter(host);
        if (!adapterResult.IsSuccess)
        {
            diagnostics.Add(SkillDoctorDiagnostic.Error(
                adapterResult.Failure!.Code,
                adapterResult.Failure.Message));
            return new SkillDoctorResult(host, fullTargetRoot, diagnostics);
        }

        var hostKey = adapterResult.Value!.Descriptor.HostKey;
        if (!Directory.Exists(fullTargetRoot))
        {
            diagnostics.Add(SkillDoctorDiagnostic.Error(
                SkillFailureCodes.InstallTargetUnmanaged,
                $"Target root does not exist: {fullTargetRoot}"));
            return new SkillDoctorResult(hostKey, fullTargetRoot, diagnostics);
        }

        foreach (var package in packages.OrderBy(static package => package.SkillName, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await DiagnosePackageAsync(package, hostKey, fullTargetRoot, diagnostics, cancellationToken).ConfigureAwait(false);
        }

        if (diagnostics.Count == 0)
        {
            diagnostics.Add(SkillDoctorDiagnostic.Info(
                "SKILL_DOCTOR_OK",
                "All official SKILL packages are installed for the requested host."));
        }

        return new SkillDoctorResult(hostKey, fullTargetRoot, diagnostics);
    }

    private async ValueTask DiagnosePackageAsync (
        CanonicalSkillPackage package,
        string host,
        string targetRoot,
        List<SkillDoctorDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var skillDirectoryResult = SkillPackagePathBoundary.ResolvePackageDirectory(targetRoot, package.SkillName);
        if (!skillDirectoryResult.IsSuccess)
        {
            diagnostics.Add(SkillDoctorDiagnostic.Error(skillDirectoryResult.Failure!.Code, skillDirectoryResult.Failure.Message, package.SkillName));
            return;
        }

        var skillDirectory = skillDirectoryResult.Value!;
        if (!Directory.Exists(skillDirectory))
        {
            diagnostics.Add(SkillDoctorDiagnostic.Error(SkillFailureCodes.InstallTargetUnmanaged, "Skill directory is missing.", package.SkillName));
            return;
        }

        var validationResult = await installedPackageValidator.ValidateAsync(package, skillDirectory, host, cancellationToken).ConfigureAwait(false);
        if (!validationResult.IsSuccess)
        {
            diagnostics.Add(SkillDoctorDiagnostic.Error(validationResult.Failure!.Code, validationResult.Failure.Message, package.SkillName));
        }
    }
}
