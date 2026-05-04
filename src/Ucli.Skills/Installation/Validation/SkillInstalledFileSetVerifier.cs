using MackySoft.Ucli.Skills.Packaging;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Installation.Validation;

/// <summary> Verifies that an installed SKILL directory contains exactly the expected materialized files. </summary>
public sealed class SkillInstalledFileSetVerifier
{
    /// <summary> Checks the installed file set against one materialized package. </summary>
    /// <param name="skillDirectory"> The installed skill directory. </param>
    /// <param name="expectedFiles"> The host-materialized file set expected for this directory. </param>
    /// <returns> <see langword="true" /> when the installed file set is exact; otherwise <see langword="false" />. </returns>
    public SkillOperationResult<bool> MatchesExpectedFiles (
        string skillDirectory,
        IReadOnlyCollection<SkillPackageFile> expectedFiles)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillDirectory);
        ArgumentNullException.ThrowIfNull(expectedFiles);

        var expectedRelativePaths = expectedFiles
            .Select(static file => file.RelativePath)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var expectedRelativePath in expectedRelativePaths.Order(StringComparer.Ordinal))
        {
            var expectedPathResult = SkillPackagePathBoundary.ResolvePackageFilePath(skillDirectory, expectedRelativePath);
            if (!expectedPathResult.IsSuccess)
            {
                return SkillOperationResult<bool>.FailureResult(expectedPathResult.Failure!.Code, expectedPathResult.Failure.Message);
            }

            if (!File.Exists(expectedPathResult.Value!))
            {
                return SkillOperationResult<bool>.Success(false);
            }
        }

        foreach (var filePath in Directory.EnumerateFiles(skillDirectory, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var filePathResult = SkillPackagePathBoundary.ResolveUnderRoot(skillDirectory, filePath);
            if (!filePathResult.IsSuccess)
            {
                return SkillOperationResult<bool>.FailureResult(filePathResult.Failure!.Code, filePathResult.Failure.Message);
            }

            var relativePath = Path.GetRelativePath(skillDirectory, filePathResult.Value!).Replace(Path.DirectorySeparatorChar, '/');
            if (!expectedRelativePaths.Contains(relativePath))
            {
                return SkillOperationResult<bool>.Success(false);
            }
        }

        return SkillOperationResult<bool>.Success(true);
    }
}
