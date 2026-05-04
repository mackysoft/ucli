using MackySoft.Ucli.Skills.Digests;
using MackySoft.Ucli.Skills.Generation;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Verifies installed host materialization against canonical host-independent content. </summary>
public sealed class SkillInstalledContentDigestVerifier
{
    private readonly SkillDigestCalculator digestCalculator;

    /// <summary> Initializes a new instance of the <see cref="SkillInstalledContentDigestVerifier" /> class. </summary>
    /// <param name="digestCalculator"> The digest calculator. </param>
    public SkillInstalledContentDigestVerifier (SkillDigestCalculator? digestCalculator = null)
    {
        this.digestCalculator = digestCalculator ?? new SkillDigestCalculator();
    }

    /// <summary> Checks whether installed files match the canonical content digest. </summary>
    /// <param name="skillDirectory"> The installed skill directory. </param>
    /// <param name="package"> The canonical package. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> <see langword="true" /> when installed content matches; otherwise <see langword="false" />. </returns>
    public async ValueTask<SkillOperationResult<bool>> MatchesContentDigestAsync (
        string skillDirectory,
        CanonicalSkillPackage package,
        IReadOnlyCollection<SkillPackageFile> expectedFiles,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillDirectory);
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(expectedFiles);
        cancellationToken.ThrowIfCancellationRequested();

        var expectedRelativePaths = expectedFiles
            .Select(static file => file.RelativePath)
            .ToHashSet(StringComparer.Ordinal);
        var unexpectedFilesResult = FindUnexpectedFiles(skillDirectory, expectedRelativePaths);
        if (!unexpectedFilesResult.IsSuccess)
        {
            return SkillOperationResult<bool>.FailureResult(unexpectedFilesResult.Failure!.Code, unexpectedFilesResult.Failure.Message);
        }

        if (unexpectedFilesResult.Value)
        {
            return SkillOperationResult<bool>.Success(false);
        }

        var digestInputs = new List<SkillDigestInputFile>();
        var skillBodyResult = await ReadInstalledSkillBodyAsync(skillDirectory, cancellationToken).ConfigureAwait(false);
        if (!skillBodyResult.IsSuccess)
        {
            return SkillOperationResult<bool>.FailureResult(skillBodyResult.Failure!.Code, skillBodyResult.Failure.Message);
        }

        if (skillBodyResult.Value is null)
        {
            return SkillOperationResult<bool>.Success(false);
        }

        digestInputs.Add(new SkillDigestInputFile("SKILL.md", skillBodyResult.Value));
        foreach (var reference in package.Files
            .Where(static file => file.RelativePath.StartsWith("references/", StringComparison.Ordinal))
            .OrderBy(static file => file.RelativePath, StringComparer.Ordinal))
        {
            var referencePathResult = SkillPathBoundary.ResolvePackageFilePath(skillDirectory, reference.RelativePath);
            if (!referencePathResult.IsSuccess)
            {
                return SkillOperationResult<bool>.FailureResult(referencePathResult.Failure!.Code, referencePathResult.Failure.Message);
            }

            if (!File.Exists(referencePathResult.Value!))
            {
                return SkillOperationResult<bool>.Success(false);
            }

            var content = SkillTextNormalizer.NormalizeToLf(await File.ReadAllTextAsync(referencePathResult.Value!, cancellationToken).ConfigureAwait(false));
            digestInputs.Add(new SkillDigestInputFile(reference.RelativePath, content));
        }

        var actualDigest = digestCalculator.ComputeDigest(digestInputs);
        return SkillOperationResult<bool>.Success(string.Equals(actualDigest, package.Manifest.ContentDigest, StringComparison.Ordinal));
    }

    private static SkillOperationResult<bool> FindUnexpectedFiles (
        string skillDirectory,
        IReadOnlySet<string> expectedRelativePaths)
    {
        foreach (var filePath in Directory.EnumerateFiles(skillDirectory, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var filePathResult = SkillPathBoundary.ResolveUnderRoot(skillDirectory, filePath);
            if (!filePathResult.IsSuccess)
            {
                return SkillOperationResult<bool>.FailureResult(filePathResult.Failure!.Code, filePathResult.Failure.Message);
            }

            var relativePath = Path.GetRelativePath(skillDirectory, filePathResult.Value!).Replace(Path.DirectorySeparatorChar, '/');
            if (!expectedRelativePaths.Contains(relativePath))
            {
                return SkillOperationResult<bool>.Success(true);
            }
        }

        return SkillOperationResult<bool>.Success(false);
    }

    private static async ValueTask<SkillOperationResult<string?>> ReadInstalledSkillBodyAsync (
        string skillDirectory,
        CancellationToken cancellationToken)
    {
        var skillPathResult = SkillPathBoundary.ResolvePackageFilePath(skillDirectory, "SKILL.md");
        if (!skillPathResult.IsSuccess)
        {
            return SkillOperationResult<string?>.FailureResult(skillPathResult.Failure!.Code, skillPathResult.Failure.Message);
        }

        if (!File.Exists(skillPathResult.Value!))
        {
            return SkillOperationResult<string?>.Success(null);
        }

        var skillText = SkillTextNormalizer.NormalizeToLf(await File.ReadAllTextAsync(skillPathResult.Value!, cancellationToken).ConfigureAwait(false));
        if (!SkillHostMaterializationInspector.TryExtractFrontmatter(skillText, out var frontmatter))
        {
            return SkillOperationResult<string?>.Success(null);
        }

        var body = skillText[frontmatter.Length..];
        if (body.StartsWith('\n'))
        {
            body = body[1..];
        }

        return SkillOperationResult<string?>.Success(body);
    }
}
