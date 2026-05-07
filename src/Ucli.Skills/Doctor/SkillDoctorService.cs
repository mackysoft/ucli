using MackySoft.Ucli.Skills.Hosts.Registration;
using MackySoft.Ucli.Skills.Installation;
using MackySoft.Ucli.Skills.Installation.Validation;
using MackySoft.Ucli.Skills.Packaging;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Doctor;

/// <summary> Diagnoses host-materialized SKILL package directories. </summary>
public sealed class SkillDoctorService
{
    private readonly SkillHostAdapterSet hostAdapters;
    private readonly SkillInstalledTargetStateAnalyzer targetStateAnalyzer;
    private readonly SkillInstalledPackageDriftAnalyzer driftAnalyzer;

    /// <summary> Initializes a new instance of the <see cref="SkillDoctorService" /> class. </summary>
    /// <param name="hostAdapters"> The supported host adapter set. </param>
    /// <param name="targetStateAnalyzer"> The installed target state analyzer. </param>
    /// <param name="driftAnalyzer"> The local drift analyzer. </param>
    public SkillDoctorService (
        SkillHostAdapterSet hostAdapters,
        SkillInstalledTargetStateAnalyzer targetStateAnalyzer,
        SkillInstalledPackageDriftAnalyzer driftAnalyzer)
    {
        this.hostAdapters = hostAdapters ?? throw new ArgumentNullException(nameof(hostAdapters));
        this.targetStateAnalyzer = targetStateAnalyzer ?? throw new ArgumentNullException(nameof(targetStateAnalyzer));
        this.driftAnalyzer = driftAnalyzer ?? throw new ArgumentNullException(nameof(driftAnalyzer));
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

        foreach (var package in packages.OrderBy(static package => package.Manifest.SkillName, StringComparer.Ordinal))
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
        var skillDirectoryResult = SkillPackagePathBoundary.ResolvePackageDirectory(targetRoot, package.Manifest.SkillName);
        if (!skillDirectoryResult.IsSuccess)
        {
            diagnostics.Add(SkillDoctorDiagnostic.Error(skillDirectoryResult.Failure!.Code, skillDirectoryResult.Failure.Message, package.Manifest.SkillName));
            return;
        }

        var skillDirectory = skillDirectoryResult.Value!;
        if (!Directory.Exists(skillDirectory))
        {
            diagnostics.Add(SkillDoctorDiagnostic.Error(SkillFailureCodes.InstallTargetUnmanaged, "Skill directory is missing.", package.Manifest.SkillName));
            return;
        }

        var stateResult = await targetStateAnalyzer.AnalyzeAsync(package, skillDirectory, host, cancellationToken).ConfigureAwait(false);
        if (!stateResult.IsSuccess)
        {
            diagnostics.Add(SkillDoctorDiagnostic.Error(stateResult.Failure!.Code, stateResult.Failure.Message, package.Manifest.SkillName));
            return;
        }

        switch (stateResult.Value!.Kind)
        {
            case SkillInstalledTargetStateKind.Current:
                return;
            case SkillInstalledTargetStateKind.Missing:
                diagnostics.Add(SkillDoctorDiagnostic.Error(SkillFailureCodes.InstallTargetUnmanaged, "Skill directory is missing.", package.Manifest.SkillName));
                return;
            case SkillInstalledTargetStateKind.CleanOutdated:
                diagnostics.Add(SkillDoctorDiagnostic.Error(
                    SkillFailureCodes.InstallTargetOutdated,
                    "Installed SKILL package is clean but older than the bundled official package.",
                    package.Manifest.SkillName));
                return;
            case SkillInstalledTargetStateKind.Unmanaged:
                diagnostics.Add(SkillDoctorDiagnostic.Error(
                    SkillFailureCodes.InstallTargetUnmanaged,
                    "Skill directory is not managed by uCLI.",
                    package.Manifest.SkillName));
                return;
            case SkillInstalledTargetStateKind.LocalModified:
                var driftResult = await driftAnalyzer.AnalyzeAsync(package, skillDirectory, host, cancellationToken).ConfigureAwait(false);
                if (!driftResult.IsSuccess)
                {
                    diagnostics.Add(SkillDoctorDiagnostic.Error(driftResult.Failure!.Code, driftResult.Failure.Message, package.Manifest.SkillName));
                    return;
                }

                diagnostics.Add(SkillDoctorDiagnostic.Error(
                    driftResult.Value!.Code,
                    driftResult.Value.Message,
                    package.Manifest.SkillName));
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(stateResult), stateResult.Value.Kind, "Unsupported target state.");
        }
    }
}
