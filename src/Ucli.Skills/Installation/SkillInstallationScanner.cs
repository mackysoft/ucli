using MackySoft.Ucli.Skills.Hosts;
using MackySoft.Ucli.Skills.Manifests;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Scans installed SKILL manifests under one host target root. </summary>
public sealed class SkillInstallationScanner
{
    private readonly SkillManifestJsonSerializer manifestSerializer;
    private readonly SkillManifestValidator manifestValidator;

    /// <summary> Initializes a new instance of the <see cref="SkillInstallationScanner" /> class. </summary>
    /// <param name="manifestSerializer"> The manifest serializer. </param>
    /// <param name="manifestValidator"> The manifest validator. </param>
    public SkillInstallationScanner (
        SkillManifestJsonSerializer? manifestSerializer = null,
        SkillManifestValidator? manifestValidator = null)
    {
        this.manifestSerializer = manifestSerializer ?? new SkillManifestJsonSerializer();
        this.manifestValidator = manifestValidator ?? new SkillManifestValidator();
    }

    /// <summary> Scans installed SKILL manifests. </summary>
    /// <param name="targetRoot"> The host target root. </param>
    /// <param name="host"> The host used for install identity. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The installed skill list or manifest failure. </returns>
    public async ValueTask<SkillOperationResult<IReadOnlyList<SkillInstalledSkill>>> ScanAsync (
        string targetRoot,
        SkillHostKind host,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetRoot);
        cancellationToken.ThrowIfCancellationRequested();

        if (!Directory.Exists(targetRoot))
        {
            return SkillOperationResult<IReadOnlyList<SkillInstalledSkill>>.Success(Array.Empty<SkillInstalledSkill>());
        }

        var installedSkills = new List<SkillInstalledSkill>();
        foreach (var manifestPath in Directory.EnumerateFiles(targetRoot, "ucli-skill.json", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var manifestText = await File.ReadAllTextAsync(manifestPath, cancellationToken).ConfigureAwait(false);
            SkillManifest manifest;
            try
            {
                manifest = manifestSerializer.Deserialize(manifestText);
            }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidOperationException)
            {
                return SkillOperationResult<IReadOnlyList<SkillInstalledSkill>>.FailureResult(
                    SkillFailureCodes.ManifestInvalid,
                    $"Invalid ucli-skill.json: {manifestPath}");
            }

            var validationResult = manifestValidator.Validate(manifest);
            if (!validationResult.IsSuccess)
            {
                return SkillOperationResult<IReadOnlyList<SkillInstalledSkill>>.FailureResult(
                    validationResult.Failure!.Code,
                    validationResult.Failure.Message);
            }

            var skillDirectory = Path.GetDirectoryName(manifestPath) ?? targetRoot;
            installedSkills.Add(new SkillInstalledSkill(
                new SkillInstallIdentity(host, SkillScopeKind.Project, Path.GetFullPath(targetRoot), manifest.SkillName),
                skillDirectory,
                manifest));
        }

        return SkillOperationResult<IReadOnlyList<SkillInstalledSkill>>.Success(installedSkills);
    }
}
