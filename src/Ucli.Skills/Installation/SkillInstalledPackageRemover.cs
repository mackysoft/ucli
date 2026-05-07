using MackySoft.Ucli.Skills.Packaging;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Deletes installed SKILL package directories under a resolved host target root. </summary>
public sealed class SkillInstalledPackageRemover : ISkillInstalledPackageRemover
{
    /// <inheritdoc />
    public ValueTask<SkillOperationResult<bool>> DeleteAsync (
        string targetRoot,
        string skillDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(skillDirectory);
        cancellationToken.ThrowIfCancellationRequested();

        var targetRootResult = SkillPackagePathBoundary.ResolveUnderRoot(targetRoot, targetRoot);
        if (!targetRootResult.IsSuccess)
        {
            return ValueTask.FromResult(SkillOperationResult<bool>.FailureResult(
                targetRootResult.Failure!.Code,
                targetRootResult.Failure.Message));
        }

        var resolvedTargetRoot = targetRootResult.Value!;
        var skillDirectoryResult = SkillPackagePathBoundary.ResolveUnderRoot(resolvedTargetRoot, skillDirectory);
        if (!skillDirectoryResult.IsSuccess)
        {
            return ValueTask.FromResult(SkillOperationResult<bool>.FailureResult(
                skillDirectoryResult.Failure!.Code,
                skillDirectoryResult.Failure.Message));
        }

        var resolvedSkillDirectory = skillDirectoryResult.Value!;
        if (IsSamePath(resolvedTargetRoot, resolvedSkillDirectory))
        {
            return ValueTask.FromResult(SkillOperationResult<bool>.FailureResult(
                SkillFailureCodes.PathUnsafe,
                $"Skill directory must not be the target root: {resolvedSkillDirectory}"));
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Directory.Exists(resolvedSkillDirectory))
            {
                Directory.Delete(resolvedSkillDirectory, recursive: true);
            }

            return ValueTask.FromResult(SkillOperationResult<bool>.Success(true));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ValueTask.FromResult(SkillOperationResult<bool>.FailureResult(
                SkillFailureCodes.InstallTargetWriteFailed,
                $"Failed to delete installed SKILL package: {resolvedSkillDirectory}. {ex.Message}"));
        }
    }

    private static bool IsSamePath (
        string left,
        string right)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return string.Equals(
            Path.TrimEndingDirectorySeparator(left),
            Path.TrimEndingDirectorySeparator(right),
            comparison);
    }
}
