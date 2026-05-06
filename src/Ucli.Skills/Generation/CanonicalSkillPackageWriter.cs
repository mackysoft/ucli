using MackySoft.Ucli.Skills.Packaging;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Generation;

/// <summary> Writes generated canonical SKILL packages to a <c>skills</c> directory. </summary>
public sealed class CanonicalSkillPackageWriter
{
    /// <summary> Writes all packages to the output root. </summary>
    /// <param name="packages"> The generated canonical packages. </param>
    /// <param name="outputRoot"> The output <c>skills</c> directory. </param>
    /// <param name="cleanOutputRoot"> Whether to remove the existing output root before writing. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The full output root path or failure. </returns>
    public async ValueTask<SkillOperationResult<string>> WriteAllAsync (
        IReadOnlyList<CanonicalSkillPackage> packages,
        string outputRoot,
        bool cleanOutputRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packages);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputRoot);
        cancellationToken.ThrowIfCancellationRequested();

        var fullOutputRoot = Path.GetFullPath(outputRoot);
        if (cleanOutputRoot)
        {
            var cleanResult = CleanOutputRoot(fullOutputRoot);
            if (!cleanResult.IsSuccess)
            {
                return SkillOperationResult<string>.FailureResult(cleanResult.Failure!.Code, cleanResult.Failure.Message);
            }
        }

        Directory.CreateDirectory(fullOutputRoot);
        foreach (var package in packages.OrderBy(static package => package.SkillName, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var skillDirectoryResult = SkillPackagePathBoundary.ResolvePackageDirectory(fullOutputRoot, package.SkillName);
            if (!skillDirectoryResult.IsSuccess)
            {
                return SkillOperationResult<string>.FailureResult(skillDirectoryResult.Failure!.Code, skillDirectoryResult.Failure.Message);
            }

            var skillDirectory = skillDirectoryResult.Value!;
            foreach (var file in package.Files.OrderBy(static file => file.RelativePath, StringComparer.Ordinal))
            {
                var filePathResult = SkillPackagePathBoundary.ResolvePackageFilePathUnderRoot(fullOutputRoot, skillDirectory, file.RelativePath);
                if (!filePathResult.IsSuccess)
                {
                    return SkillOperationResult<string>.FailureResult(filePathResult.Failure!.Code, filePathResult.Failure.Message);
                }

                await SkillPackageFileWriter.WriteAllTextAtomically(filePathResult.Value!, file.Content, cancellationToken).ConfigureAwait(false);
            }
        }

        return SkillOperationResult<string>.Success(fullOutputRoot);
    }

    private static SkillOperationResult<bool> CleanOutputRoot (string outputRoot)
    {
        if (!string.Equals(Path.GetFileName(outputRoot), "skills", StringComparison.Ordinal))
        {
            return SkillOperationResult<bool>.FailureResult(
                SkillFailureCodes.PathUnsafe,
                $"Generated SKILL output root must be named 'skills': {outputRoot}");
        }

        if (Directory.Exists(outputRoot))
        {
            Directory.Delete(outputRoot, recursive: true);
        }

        return SkillOperationResult<bool>.Success(true);
    }
}
