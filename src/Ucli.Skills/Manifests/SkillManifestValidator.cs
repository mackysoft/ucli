using MackySoft.Ucli.Skills.Hosts;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Manifests;

/// <summary> Validates canonical SKILL manifests. </summary>
public sealed class SkillManifestValidator
{
    private readonly SkillHostRegistry hostRegistry;

    /// <summary> Initializes a new instance of the <see cref="SkillManifestValidator" /> class. </summary>
    /// <param name="hostRegistry"> The supported host registry. </param>
    public SkillManifestValidator (SkillHostRegistry? hostRegistry = null)
    {
        this.hostRegistry = hostRegistry ?? new SkillHostRegistry();
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

        if (!IsSha256Digest(manifest.ContentDigest))
        {
            return Failure("ucli-skill.json contentDigest must be a sha256 digest.");
        }

        var expectedHosts = hostRegistry.Descriptors.Select(static descriptor => descriptor.HostName).ToArray();
        var artifactHosts = manifest.HostArtifacts.Select(static artifact => artifact.Host).Order(StringComparer.Ordinal).ToArray();
        if (!expectedHosts.Order(StringComparer.Ordinal).SequenceEqual(artifactHosts))
        {
            return Failure("ucli-skill.json hostArtifacts must contain exactly all supported hosts.");
        }

        foreach (var artifact in manifest.HostArtifacts)
        {
            if (!IsSha256Digest(artifact.MaterializedFrontmatterDigest))
            {
                return Failure($"Host artifact '{artifact.Host}' frontmatter digest must be a sha256 digest.");
            }

            if (string.Equals(artifact.Host, SkillHostKindValues.OpenAi, StringComparison.Ordinal))
            {
                if (!string.Equals(artifact.Path, "agents/openai.yaml", StringComparison.Ordinal) || !IsSha256Digest(artifact.Digest))
                {
                    return Failure("OpenAI host artifact must contain agents/openai.yaml digest.");
                }
            }
            else if (artifact.Path is not null || artifact.Digest is not null)
            {
                return Failure($"Host artifact '{artifact.Host}' must not contain file artifact fields.");
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
