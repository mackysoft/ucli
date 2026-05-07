using MackySoft.Ucli.Skills.Packaging;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Acquires cooperative locks for SKILL package transaction directories. </summary>
internal static class SkillPackageTransactionLock
{
    /// <summary> Acquires an exclusive lock file under the transaction directory. </summary>
    /// <param name="targetRoot"> The resolved host target root. </param>
    /// <param name="transactionRoot"> The transaction directory under the target root. </param>
    /// <returns> A disposable lock handle or a write failure. </returns>
    public static SkillOperationResult<IDisposable> Acquire (
        string targetRoot,
        string transactionRoot)
    {
        var lockPathResult = SkillPackagePathBoundary.ResolveUnderRoot(targetRoot, Path.Combine(transactionRoot, ".lock"));
        if (!lockPathResult.IsSuccess)
        {
            return SkillOperationResult<IDisposable>.FailureResult(lockPathResult.Failure!.Code, lockPathResult.Failure.Message);
        }

        try
        {
            return SkillOperationResult<IDisposable>.Success(new FileStream(
                lockPathResult.Value!,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return SkillOperationResult<IDisposable>.FailureResult(
                SkillFailureCodes.InstallTargetWriteFailed,
                $"Failed to acquire SKILL package transaction lock: {lockPathResult.Value}. {ex.Message}");
        }
    }
}
