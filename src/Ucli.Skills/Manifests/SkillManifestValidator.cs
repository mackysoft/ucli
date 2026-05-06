using MackySoft.Ucli.Skills.Hosts.Registration;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Manifests;

/// <summary> Validates canonical SKILL manifests. </summary>
public sealed class SkillManifestValidator
{
    private readonly SkillHostAdapterSet hostAdapters;

    /// <summary> Initializes a new instance of the <see cref="SkillManifestValidator" /> class. </summary>
    /// <param name="hostAdapters"> The supported host adapter set. </param>
    public SkillManifestValidator (SkillHostAdapterSet hostAdapters)
    {
        this.hostAdapters = hostAdapters ?? throw new ArgumentNullException(nameof(hostAdapters));
    }

    /// <summary> Validates one manifest. </summary>
    /// <param name="manifest"> The manifest. </param>
    /// <returns> The valid manifest or validation failure. </returns>
    public SkillOperationResult<SkillManifest> Validate (SkillManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        if (manifest.SchemaVersion != SkillManifest.CurrentSchemaVersion)
        {
            return Failure($"Unsupported ucli-skill.json schemaVersion: {manifest.SchemaVersion}");
        }

        if (!IsSafeSkillName(manifest.SkillName))
        {
            return Failure("ucli-skill.json skillName must be a safe SKILL identifier.");
        }

        if (string.IsNullOrWhiteSpace(manifest.DisplayName)
            || string.IsNullOrWhiteSpace(manifest.Description)
            || manifest.Description.Length > 1024)
        {
            return Failure("ucli-skill.json displayName and description must be valid.");
        }

        if (!IsSha256Digest(manifest.ContentDigest))
        {
            return Failure("ucli-skill.json contentDigest must be a sha256 digest.");
        }

        var expectedHosts = hostAdapters.Adapters.Select(static adapter => adapter.Descriptor.HostKey).Order(StringComparer.Ordinal).ToArray();
        var artifactHosts = manifest.HostArtifacts.Select(static artifact => artifact.Host).Order(StringComparer.Ordinal).ToArray();
        if (!expectedHosts.SequenceEqual(artifactHosts))
        {
            return Failure("ucli-skill.json hostArtifacts must contain exactly all supported hosts.");
        }

        var artifactByHost = manifest.HostArtifacts.ToDictionary(static artifact => artifact.Host, StringComparer.Ordinal);
        foreach (var adapter in hostAdapters.Adapters)
        {
            var artifact = artifactByHost[adapter.Descriptor.HostKey];
            if (!IsSha256Digest(artifact.MaterializedFrontmatterDigest))
            {
                return Failure($"Host artifact '{artifact.Host}' frontmatter digest must be a sha256 digest.");
            }

            if (adapter.MetadataArtifactPath is null)
            {
                if (artifact.Path is not null || artifact.Digest is not null)
                {
                    return Failure($"Host artifact '{artifact.Host}' must not contain file artifact fields.");
                }

                continue;
            }

            if (!string.Equals(artifact.Path, adapter.MetadataArtifactPath, StringComparison.Ordinal) || !IsSha256Digest(artifact.Digest))
            {
                return Failure($"Host artifact '{artifact.Host}' must contain metadata artifact digest.");
            }
        }

        return SkillOperationResult<SkillManifest>.Success(manifest);
    }

    private static SkillOperationResult<SkillManifest> Failure (string message)
    {
        return SkillOperationResult<SkillManifest>.FailureResult(SkillFailureCodes.ManifestInvalid, message);
    }

    private static bool IsSha256Digest (string? value)
    {
        if (value is null || !value.StartsWith("sha256:", StringComparison.Ordinal) || value.Length != 71)
        {
            return false;
        }

        return value.AsSpan("sha256:".Length).IndexOfAnyExcept("0123456789abcdef") < 0;
    }

    private static bool IsSafeSkillName (string? skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName) || !IsAsciiLowercaseLetterOrDigit(skillName[0]))
        {
            return false;
        }

        for (var i = 1; i < skillName.Length; i++)
        {
            var character = skillName[i];
            if (character != '-' && !IsAsciiLowercaseLetterOrDigit(character))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAsciiLowercaseLetterOrDigit (char character)
    {
        return character is (>= 'a' and <= 'z') or (>= '0' and <= '9');
    }
}
