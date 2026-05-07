using MackySoft.Ucli.Skills.Installation.Validation;
using MackySoft.Ucli.Skills.Packaging;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Classifies an installed SKILL target before write or delete operations. </summary>
public sealed class SkillInstalledTargetStateAnalyzer
{
    private readonly SkillInstalledPackageValidator installedPackageValidator;
    private readonly SkillInstalledPackageIntegrityVerifier installedPackageIntegrityVerifier;

    /// <summary> Initializes a new instance of the <see cref="SkillInstalledTargetStateAnalyzer" /> class. </summary>
    /// <param name="installedPackageValidator"> The current canonical package validator. </param>
    /// <param name="installedPackageIntegrityVerifier"> The installed package integrity verifier. </param>
    public SkillInstalledTargetStateAnalyzer (
        SkillInstalledPackageValidator installedPackageValidator,
        SkillInstalledPackageIntegrityVerifier installedPackageIntegrityVerifier)
    {
        this.installedPackageValidator = installedPackageValidator ?? throw new ArgumentNullException(nameof(installedPackageValidator));
        this.installedPackageIntegrityVerifier = installedPackageIntegrityVerifier ?? throw new ArgumentNullException(nameof(installedPackageIntegrityVerifier));
    }

    /// <summary> Analyzes one target directory against the current canonical package and requested host. </summary>
    /// <param name="package"> The canonical package. </param>
    /// <param name="skillDirectory"> The target skill directory. </param>
    /// <param name="host"> The requested host. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The target state or a hard safety failure. </returns>
    public async ValueTask<SkillOperationResult<SkillInstalledTargetState>> AnalyzeAsync (
        CanonicalSkillPackage package,
        string skillDirectory,
        string host,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentException.ThrowIfNullOrWhiteSpace(skillDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        cancellationToken.ThrowIfCancellationRequested();

        if (!Directory.Exists(skillDirectory))
        {
            return SkillOperationResult<SkillInstalledTargetState>.Success(new SkillInstalledTargetState(SkillInstalledTargetStateKind.Missing));
        }

        var currentResult = await installedPackageValidator.ValidateAsync(package, skillDirectory, host, cancellationToken).ConfigureAwait(false);
        if (currentResult.IsSuccess)
        {
            return SkillOperationResult<SkillInstalledTargetState>.Success(new SkillInstalledTargetState(
                SkillInstalledTargetStateKind.Current,
                currentResult.Value));
        }

        if (string.Equals(currentResult.Failure!.Code, SkillFailureCodes.InstallTargetUnmanaged, StringComparison.Ordinal))
        {
            return SkillOperationResult<SkillInstalledTargetState>.Success(new SkillInstalledTargetState(SkillInstalledTargetStateKind.Unmanaged));
        }

        if (!string.Equals(currentResult.Failure.Code, SkillFailureCodes.InstallTargetDigestMismatch, StringComparison.Ordinal))
        {
            return SkillOperationResult<SkillInstalledTargetState>.FailureResult(currentResult.Failure.Code, currentResult.Failure.Message);
        }

        var integrityResult = await installedPackageIntegrityVerifier.VerifyAsync(skillDirectory, host, cancellationToken).ConfigureAwait(false);
        if (integrityResult.IsSuccess)
        {
            return SkillOperationResult<SkillInstalledTargetState>.Success(new SkillInstalledTargetState(
                SkillInstalledTargetStateKind.CleanOutdated,
                integrityResult.Value));
        }

        return string.Equals(integrityResult.Failure!.Code, SkillFailureCodes.InstallTargetDigestMismatch, StringComparison.Ordinal)
            ? SkillOperationResult<SkillInstalledTargetState>.Success(new SkillInstalledTargetState(SkillInstalledTargetStateKind.LocalModified))
            : SkillOperationResult<SkillInstalledTargetState>.FailureResult(integrityResult.Failure.Code, integrityResult.Failure.Message);
    }
}
