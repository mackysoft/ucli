using MackySoft.Ucli.Skills.Hosts.Registration;
using MackySoft.Ucli.Skills.Manifests;
using MackySoft.Ucli.Skills.Packaging;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Installation.Validation;

/// <summary> Reads and validates an installed <c>ucli-skill.json</c> manifest. </summary>
public sealed class SkillInstalledManifestReader
{
    private readonly SkillManifestJsonSerializer manifestSerializer;
    private readonly SkillManifestValidator manifestValidator;

    /// <summary> Initializes a new instance of the <see cref="SkillInstalledManifestReader" /> class. </summary>
    /// <param name="hostAdapters"> The supported host adapter set. </param>
    /// <param name="manifestSerializer"> The manifest serializer. </param>
    /// <param name="manifestValidator"> The manifest validator. </param>
    public SkillInstalledManifestReader (
        SkillHostAdapterSet hostAdapters,
        SkillManifestJsonSerializer? manifestSerializer = null,
        SkillManifestValidator? manifestValidator = null)
    {
        ArgumentNullException.ThrowIfNull(hostAdapters);

        this.manifestSerializer = manifestSerializer ?? new SkillManifestJsonSerializer();
        this.manifestValidator = manifestValidator ?? new SkillManifestValidator(hostAdapters);
    }

    /// <summary> Reads and validates the required installed manifest from one skill directory. </summary>
    /// <param name="skillDirectory"> The installed skill directory. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The installed manifest or validation failure. </returns>
    public async ValueTask<SkillOperationResult<SkillInstalledManifest>> ReadRequiredAsync (
        string skillDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillDirectory);
        cancellationToken.ThrowIfCancellationRequested();

        var manifestPathResult = SkillPackagePathBoundary.ResolvePackageFilePath(skillDirectory, "ucli-skill.json");
        if (!manifestPathResult.IsSuccess)
        {
            return SkillOperationResult<SkillInstalledManifest>.FailureResult(
                manifestPathResult.Failure!.Code,
                manifestPathResult.Failure.Message);
        }

        var manifestPath = manifestPathResult.Value!;
        if (!File.Exists(manifestPath))
        {
            return SkillOperationResult<SkillInstalledManifest>.FailureResult(
                SkillFailureCodes.InstallTargetUnmanaged,
                $"Target skill directory is missing ucli-skill.json: {skillDirectory}");
        }

        var manifestText = await File.ReadAllTextAsync(manifestPath, cancellationToken).ConfigureAwait(false);
        var manifestResult = manifestSerializer.TryDeserialize(manifestText);
        if (!manifestResult.IsSuccess)
        {
            return SkillOperationResult<SkillInstalledManifest>.FailureResult(
                manifestResult.Failure!.Code,
                $"Target skill manifest is invalid: {manifestPath}");
        }

        var manifest = manifestResult.Value!;
        var validationResult = manifestValidator.Validate(manifest);
        if (!validationResult.IsSuccess)
        {
            return SkillOperationResult<SkillInstalledManifest>.FailureResult(
                validationResult.Failure!.Code,
                validationResult.Failure.Message);
        }

        if (!string.Equals(Path.GetFileName(skillDirectory), manifest.SkillName, StringComparison.Ordinal))
        {
            return SkillOperationResult<SkillInstalledManifest>.FailureResult(
                SkillFailureCodes.ManifestInvalid,
                $"ucli-skill.json skillName must match installed directory name: {manifestPath}");
        }

        return SkillOperationResult<SkillInstalledManifest>.Success(new SkillInstalledManifest(
            manifestPath,
            manifestText,
            manifest));
    }
}
