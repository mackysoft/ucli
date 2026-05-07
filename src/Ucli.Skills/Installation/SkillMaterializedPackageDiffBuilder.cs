using MackySoft.Ucli.Skills.Materialization;
using MackySoft.Ucli.Skills.Packaging;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Builds structured file diffs between an installed target and a materialized package. </summary>
public sealed class SkillMaterializedPackageDiffBuilder
{
    /// <summary> Builds one structured diff for a target directory and desired materialized package. </summary>
    /// <param name="skillDirectory"> The target skill directory. </param>
    /// <param name="materializedPackage"> The desired materialized package. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> Structured diffs or a path-safety failure. </returns>
    public async ValueTask<SkillOperationResult<IReadOnlyList<SkillActionDiff>>> BuildAsync (
        string skillDirectory,
        SkillMaterializedPackage materializedPackage,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillDirectory);
        ArgumentNullException.ThrowIfNull(materializedPackage);
        cancellationToken.ThrowIfCancellationRequested();

        var beforeResult = await ReadExistingFilesAsync(skillDirectory, cancellationToken).ConfigureAwait(false);
        if (!beforeResult.IsSuccess)
        {
            return SkillOperationResult<IReadOnlyList<SkillActionDiff>>.FailureResult(
                beforeResult.Failure!.Code,
                beforeResult.Failure.Message);
        }

        var beforeFiles = beforeResult.Value!;
        var afterFiles = materializedPackage.Files.ToDictionary(
            static file => file.RelativePath,
            static file => SkillTextNormalizer.NormalizeToLf(file.Content),
            StringComparer.Ordinal);

        var relativePaths = beforeFiles.Keys
            .Concat(afterFiles.Keys)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var fileDiffs = new List<SkillFileDiff>();
        foreach (var relativePath in relativePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var hasBefore = beforeFiles.TryGetValue(relativePath, out var beforeContent);
            var hasAfter = afterFiles.TryGetValue(relativePath, out var afterContent);
            if (hasBefore && hasAfter)
            {
                if (!string.Equals(beforeContent, afterContent, StringComparison.Ordinal))
                {
                    fileDiffs.Add(new SkillFileDiff(relativePath, SkillDiffChangeKind.Modified, beforeContent, afterContent));
                }

                continue;
            }

            if (hasAfter)
            {
                fileDiffs.Add(new SkillFileDiff(relativePath, SkillDiffChangeKind.Added, null, afterContent));
                continue;
            }

            fileDiffs.Add(new SkillFileDiff(relativePath, SkillDiffChangeKind.Deleted, beforeContent, null));
        }

        return fileDiffs.Count == 0
            ? SkillOperationResult<IReadOnlyList<SkillActionDiff>>.Success(Array.Empty<SkillActionDiff>())
            : SkillOperationResult<IReadOnlyList<SkillActionDiff>>.Success([new SkillActionDiff(fileDiffs)]);
    }

    private static async ValueTask<SkillOperationResult<Dictionary<string, string>>> ReadExistingFilesAsync (
        string skillDirectory,
        CancellationToken cancellationToken)
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!Directory.Exists(skillDirectory))
        {
            return SkillOperationResult<Dictionary<string, string>>.Success(files);
        }

        try
        {
            foreach (var filePath in Directory.EnumerateFiles(skillDirectory, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var resolvedPathResult = SkillPackagePathBoundary.ResolveUnderRoot(skillDirectory, filePath);
                if (!resolvedPathResult.IsSuccess)
                {
                    return SkillOperationResult<Dictionary<string, string>>.FailureResult(
                        resolvedPathResult.Failure!.Code,
                        resolvedPathResult.Failure.Message);
                }

                var relativePath = Path.GetRelativePath(skillDirectory, resolvedPathResult.Value!).Replace(Path.DirectorySeparatorChar, '/');
                files[relativePath] = SkillTextNormalizer.NormalizeToLf(
                    await File.ReadAllTextAsync(resolvedPathResult.Value!, cancellationToken).ConfigureAwait(false));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return SkillOperationResult<Dictionary<string, string>>.FailureResult(
                SkillFailureCodes.InstallTargetReadFailed,
                $"Failed to read SKILL package diff input: {skillDirectory}. {ex.Message}");
        }

        return SkillOperationResult<Dictionary<string, string>>.Success(files);
    }
}
