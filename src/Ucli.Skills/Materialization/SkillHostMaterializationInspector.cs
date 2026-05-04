using MackySoft.Ucli.Skills.Digests;
using MackySoft.Ucli.Skills.Hosts;
using MackySoft.Ucli.Skills.Manifests;
using MackySoft.Ucli.Skills.Packaging;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Materialization;

/// <summary> Inspects installed files to determine whether they belong to the requested host. </summary>
public sealed class SkillHostMaterializationInspector
{
    private readonly SkillHostAdapterSet hostAdapters;
    private readonly SkillDigestCalculator digestCalculator;

    /// <summary> Initializes a new instance of the <see cref="SkillHostMaterializationInspector" /> class. </summary>
    /// <param name="hostAdapters"> The supported host adapter set. </param>
    /// <param name="digestCalculator"> The digest calculator. </param>
    public SkillHostMaterializationInspector (
        SkillHostAdapterSet? hostAdapters = null,
        SkillDigestCalculator? digestCalculator = null)
    {
        this.hostAdapters = hostAdapters ?? new SkillHostAdapterSet();
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

        var adapterResult = hostAdapters.GetAdapter(host);
        if (!adapterResult.IsSuccess)
        {
            return SkillOperationResult<bool>.FailureResult(
                adapterResult.Failure!.Code,
                adapterResult.Failure.Message);
        }

        var adapter = adapterResult.Value!;
        var hostName = adapter.Descriptor.HostName;
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

        if (adapter.MetadataArtifactPath is null)
        {
            return expectedArtifact.Path is null && expectedArtifact.Digest is null
                ? SkillOperationResult<bool>.Success(true)
                : SkillOperationResult<bool>.FailureResult(
                    SkillFailureCodes.ManifestInvalid,
                    $"Manifest host artifact '{hostName}' must not contain metadata artifact fields.");
        }

        var metadataArtifactPath = expectedArtifact.Path;
        var metadataArtifactDigest = expectedArtifact.Digest;
        if (metadataArtifactPath is null
            || !string.Equals(metadataArtifactPath, adapter.MetadataArtifactPath, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(metadataArtifactDigest))
        {
            return SkillOperationResult<bool>.FailureResult(
                SkillFailureCodes.ManifestInvalid,
                $"Manifest host artifact '{hostName}' must contain metadata artifact fields.");
        }

        var metadataPathResult = SkillPackagePathBoundary.ResolvePackageFilePath(skillDirectory, metadataArtifactPath);
        if (!metadataPathResult.IsSuccess)
        {
            return SkillOperationResult<bool>.FailureResult(metadataPathResult.Failure!.Code, metadataPathResult.Failure.Message);
        }

        if (!File.Exists(metadataPathResult.Value!))
        {
            return SkillOperationResult<bool>.Success(false);
        }

        var metadata = SkillTextNormalizer.NormalizeToLf(await File.ReadAllTextAsync(metadataPathResult.Value!, cancellationToken).ConfigureAwait(false));
        var actualDigest = digestCalculator.ComputeSingleFileDigest(metadataArtifactPath, metadata);
        return SkillOperationResult<bool>.Success(string.Equals(actualDigest, metadataArtifactDigest, StringComparison.Ordinal));
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
