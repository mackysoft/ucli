using MackySoft.Ucli.Skills.Packaging;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Validates transaction directories before file-system writes or cleanup. </summary>
internal static class SkillPackageTransactionPathGuard
{
    /// <summary> Verifies that a created transaction directory is a regular directory under the target root. </summary>
    /// <param name="targetRoot"> The resolved host target root. </param>
    /// <param name="directoryPath"> The transaction directory path. </param>
    /// <returns> Success when the directory is not a link and resolves under the target root. </returns>
    public static SkillOperationResult<bool> ValidateCreatedDirectory (
        string targetRoot,
        string directoryPath)
    {
        var resolvedResult = SkillPackagePathBoundary.ResolveUnderRoot(targetRoot, directoryPath);
        if (!resolvedResult.IsSuccess)
        {
            return SkillOperationResult<bool>.FailureResult(resolvedResult.Failure!.Code, resolvedResult.Failure.Message);
        }

        var directory = new DirectoryInfo(directoryPath);
        if (!directory.Exists)
        {
            return SkillOperationResult<bool>.FailureResult(
                SkillFailureCodes.InstallTargetWriteFailed,
                $"SKILL package transaction directory is missing: {directoryPath}");
        }

        if (directory.LinkTarget is not null || (directory.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            return SkillOperationResult<bool>.FailureResult(
                SkillFailureCodes.PathUnsafe,
                $"SKILL package transaction directory must not be a link: {directoryPath}");
        }

        return SkillOperationResult<bool>.Success(true);
    }
}
