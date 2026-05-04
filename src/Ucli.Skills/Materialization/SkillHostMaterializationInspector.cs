using MackySoft.Ucli.Skills.Digests;
using MackySoft.Ucli.Skills.Hosts;
using MackySoft.Ucli.Skills.Manifests;
using MackySoft.Ucli.Skills.Packaging;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Materialization;

/// <summary> Inspects installed files to determine whether they belong to the requested host. </summary>
public sealed class SkillHostMaterializationInspector
{
    private readonly SkillDigestCalculator digestCalculator;

    /// <summary> Initializes a new instance of the <see cref="SkillHostMaterializationInspector" /> class. </summary>
    /// <param name="digestCalculator"> The digest calculator. </param>
    public SkillHostMaterializationInspector (SkillDigestCalculator? digestCalculator = null)
    {
        this.digestCalculator = digestCalculator ?? new SkillDigestCalculator();
    }

    /// <summary> Determines whether a skill directory is materialized for the requested host. </summary>
    /// <param name="skillDirectory"> The skill directory. </param>
    /// <param name="manifest"> The canonical manifest. </param>
    /// <param name="host"> The requested host. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> <see langword="true" /> when files match the requested host; otherwise <see langword="false" />. </returns>
    public async ValueTask<SkillOperationResult<bool>> MatchesHostAsync (
        string skillDirectory,
        SkillManifest manifest,
        SkillHostKind host,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillDirectory);
        ArgumentNullException.ThrowIfNull(manifest);
        cancellationToken.ThrowIfCancellationRequested();

        if (!SkillHostKindCodec.TryToValue(host, out var hostName))
        {
            return SkillOperationResult<bool>.FailureResult(
                SkillFailureCodes.HostUnsupported,
                $"Unsupported SKILL host: {host}");
        }

        var expectedArtifact = manifest.HostArtifacts.SingleOrDefault(artifact => string.Equals(artifact.Host, hostName, StringComparison.Ordinal));
        if (expectedArtifact is null)
        {
            return SkillOperationResult<bool>.FailureResult(SkillFailureCodes.ManifestInvalid, $"Manifest does not contain host artifact '{hostName}'.");
        }

        var skillPathResult = SkillPackagePathBoundary.ResolvePackageFilePath(skillDirectory, "SKILL.md");
        if (!skillPathResult.IsSuccess)
        {
            return SkillOperationResult<bool>.FailureResult(skillPathResult.Failure!.Code, skillPathResult.Failure.Message);
        }

        var skillPath = skillPathResult.Value!;
        if (!File.Exists(skillPath))
        {
            return SkillOperationResult<bool>.Success(false);
        }

        var skillText = SkillTextNormalizer.NormalizeToLf(await File.ReadAllTextAsync(skillPath, cancellationToken).ConfigureAwait(false));
        if (!TryExtractFrontmatter(skillText, out var frontmatter))
        {
            return SkillOperationResult<bool>.Success(false);
        }

        var actualFrontmatterDigest = digestCalculator.ComputeSingleFileDigest("SKILL.md.frontmatter", frontmatter);
        if (!string.Equals(actualFrontmatterDigest, expectedArtifact.MaterializedFrontmatterDigest, StringComparison.Ordinal))
        {
            return SkillOperationResult<bool>.Success(false);
        }

        var openAiMetadataPathResult = SkillPackagePathBoundary.ResolvePackageFilePath(skillDirectory, "agents/openai.yaml");
        if (!openAiMetadataPathResult.IsSuccess)
        {
            return SkillOperationResult<bool>.FailureResult(openAiMetadataPathResult.Failure!.Code, openAiMetadataPathResult.Failure.Message);
        }

        var openAiMetadataPath = openAiMetadataPathResult.Value!;
        if (host == SkillHostKind.OpenAi)
        {
            if (!File.Exists(openAiMetadataPath) || string.IsNullOrWhiteSpace(expectedArtifact.Path) || string.IsNullOrWhiteSpace(expectedArtifact.Digest))
            {
                return SkillOperationResult<bool>.Success(false);
            }

            var openAiMetadata = SkillTextNormalizer.NormalizeToLf(await File.ReadAllTextAsync(openAiMetadataPath, cancellationToken).ConfigureAwait(false));
            var actualDigest = digestCalculator.ComputeSingleFileDigest(expectedArtifact.Path, openAiMetadata);
            return SkillOperationResult<bool>.Success(string.Equals(actualDigest, expectedArtifact.Digest, StringComparison.Ordinal));
        }

        return SkillOperationResult<bool>.Success(!File.Exists(openAiMetadataPath));
    }

    /// <summary> Extracts YAML frontmatter from a materialized <c>SKILL.md</c>. </summary>
    /// <param name="text"> The SKILL.md text. </param>
    /// <param name="frontmatter"> The extracted frontmatter. </param>
    /// <returns> <see langword="true" /> when frontmatter exists; otherwise <see langword="false" />. </returns>
    public static bool TryExtractFrontmatter (
        string text,
        out string frontmatter)
    {
        ArgumentNullException.ThrowIfNull(text);
        frontmatter = string.Empty;

        var normalized = SkillTextNormalizer.NormalizeToLf(text);
        if (!normalized.StartsWith("---\n", StringComparison.Ordinal))
        {
            return false;
        }

        var closingIndex = normalized.IndexOf("\n---\n", 4, StringComparison.Ordinal);
        if (closingIndex < 0)
        {
            return false;
        }

        frontmatter = normalized[..(closingIndex + "\n---\n".Length)];
        return true;
    }
}
