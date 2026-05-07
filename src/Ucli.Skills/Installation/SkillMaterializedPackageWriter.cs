using MackySoft.Ucli.Skills.Materialization;
using MackySoft.Ucli.Skills.Packaging;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Writes materialized SKILL packages under a resolved host target root. </summary>
internal static class SkillMaterializedPackageWriter
{
    /// <summary> Writes all files for one materialized package. </summary>
    /// <param name="targetRoot"> The resolved host target root. </param>
    /// <param name="skillDirectory"> The resolved skill package directory. </param>
    /// <param name="materializedPackage"> The materialized package to write. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> Success when all file paths stay under the target root; otherwise a path-safety failure. </returns>
    public static async ValueTask<SkillOperationResult<bool>> WriteAsync (
        string targetRoot,
        string skillDirectory,
        SkillMaterializedPackage materializedPackage,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(skillDirectory);
        ArgumentNullException.ThrowIfNull(materializedPackage);
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var file in materializedPackage.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var filePathResult = SkillPackagePathBoundary.ResolvePackageFilePathUnderRoot(targetRoot, skillDirectory, file.RelativePath);
            if (!filePathResult.IsSuccess)
            {
                return SkillOperationResult<bool>.FailureResult(filePathResult.Failure!.Code, filePathResult.Failure.Message);
            }

            await SkillPackageFileWriter.WriteAllTextAtomically(filePathResult.Value!, file.Content, cancellationToken).ConfigureAwait(false);
        }

        return SkillOperationResult<bool>.Success(true);
    }
}
