using MackySoft.Ucli.Skills.Packaging;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Deletes installed SKILL package directories under a resolved host target root. </summary>
public sealed class SkillInstalledPackageRemover : ISkillInstalledPackageRemover
{
    private readonly ISkillPackageDirectoryOperations directoryOperations;

    /// <summary> Initializes a new instance of the <see cref="SkillInstalledPackageRemover" /> class. </summary>
    public SkillInstalledPackageRemover ()
        : this(new SkillPackageDirectoryOperations())
    {
    }

    internal SkillInstalledPackageRemover (ISkillPackageDirectoryOperations directoryOperations)
    {
        this.directoryOperations = directoryOperations ?? throw new ArgumentNullException(nameof(directoryOperations));
    }

    /// <inheritdoc />
    public async ValueTask<SkillOperationResult<bool>> DeleteAsync (
        string targetRoot,
        string skillDirectory,
        Func<string, CancellationToken, ValueTask<SkillOperationResult<bool>>>? precondition,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(skillDirectory);
        cancellationToken.ThrowIfCancellationRequested();

        var targetRootResult = SkillPackagePathBoundary.ResolveUnderRoot(targetRoot, targetRoot);
        if (!targetRootResult.IsSuccess)
        {
            return SkillOperationResult<bool>.FailureResult(
                targetRootResult.Failure!.Code,
                targetRootResult.Failure.Message);
        }

        var resolvedTargetRoot = targetRootResult.Value!;
        var skillDirectoryResult = SkillPackagePathBoundary.ResolveUnderRoot(resolvedTargetRoot, skillDirectory);
        if (!skillDirectoryResult.IsSuccess)
        {
            return SkillOperationResult<bool>.FailureResult(
                skillDirectoryResult.Failure!.Code,
                skillDirectoryResult.Failure.Message);
        }

        var resolvedSkillDirectory = skillDirectoryResult.Value!;
        if (IsSamePath(resolvedTargetRoot, resolvedSkillDirectory))
        {
            return SkillOperationResult<bool>.FailureResult(
                SkillFailureCodes.PathUnsafe,
                $"Skill directory must not be the target root: {resolvedSkillDirectory}");
        }

        if (!directoryOperations.Exists(resolvedSkillDirectory))
        {
            return SkillOperationResult<bool>.Success(true);
        }

        var parentDirectory = Path.GetDirectoryName(resolvedSkillDirectory);
        if (string.IsNullOrWhiteSpace(parentDirectory))
        {
            return SkillOperationResult<bool>.FailureResult(
                SkillFailureCodes.InstallTargetWriteFailed,
                $"Skill directory parent could not be resolved: {resolvedSkillDirectory}");
        }

        var transactionRootResult = SkillPackagePathBoundary.ResolveUnderRoot(
            resolvedTargetRoot,
            Path.Combine(parentDirectory, ".ucli-skill-transactions"));
        if (!transactionRootResult.IsSuccess)
        {
            return SkillOperationResult<bool>.FailureResult(
                transactionRootResult.Failure!.Code,
                transactionRootResult.Failure.Message);
        }

        var deletedContainerResult = SkillPackagePathBoundary.ResolveUnderRoot(
            resolvedTargetRoot,
            Path.Combine(transactionRootResult.Value!, $"{Path.GetFileName(resolvedSkillDirectory)}.delete.{Guid.NewGuid():N}"));
        if (!deletedContainerResult.IsSuccess)
        {
            return SkillOperationResult<bool>.FailureResult(
                deletedContainerResult.Failure!.Code,
                deletedContainerResult.Failure.Message);
        }

        var deletedDirectoryResult = SkillPackagePathBoundary.ResolveUnderRoot(
            resolvedTargetRoot,
            Path.Combine(deletedContainerResult.Value!, Path.GetFileName(resolvedSkillDirectory)));
        if (!deletedDirectoryResult.IsSuccess)
        {
            return SkillOperationResult<bool>.FailureResult(
                deletedDirectoryResult.Failure!.Code,
                deletedDirectoryResult.Failure.Message);
        }

        var transactionRoot = transactionRootResult.Value!;
        var deletedContainer = deletedContainerResult.Value!;
        var deletedDirectory = deletedDirectoryResult.Value!;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            directoryOperations.Create(transactionRoot);
            var transactionRootGuard = SkillPackageTransactionPathGuard.ValidateCreatedDirectory(resolvedTargetRoot, transactionRoot);
            if (!transactionRootGuard.IsSuccess)
            {
                return SkillOperationResult<bool>.FailureResult(
                    transactionRootGuard.Failure!.Code,
                    transactionRootGuard.Failure.Message);
            }

            var lockResult = SkillPackageTransactionLock.Acquire(resolvedTargetRoot, transactionRoot);
            if (!lockResult.IsSuccess)
            {
                return SkillOperationResult<bool>.FailureResult(lockResult.Failure!.Code, lockResult.Failure.Message);
            }

            using var transactionLock = lockResult.Value!;
            if (precondition is not null)
            {
                var preconditionResult = await precondition(resolvedSkillDirectory, cancellationToken).ConfigureAwait(false);
                if (!preconditionResult.IsSuccess)
                {
                    return SkillOperationResult<bool>.FailureResult(preconditionResult.Failure!.Code, preconditionResult.Failure.Message);
                }
            }

            if (!directoryOperations.Exists(resolvedSkillDirectory))
            {
                return SkillOperationResult<bool>.FailureResult(
                    SkillFailureCodes.InstallTargetDigestMismatch,
                    $"Target skill directory changed after planning; refusing to delete: {resolvedSkillDirectory}");
            }

            directoryOperations.Create(deletedContainer);
            var deletedContainerGuard = SkillPackageTransactionPathGuard.ValidateCreatedDirectory(resolvedTargetRoot, deletedContainer);
            if (!deletedContainerGuard.IsSuccess)
            {
                return SkillOperationResult<bool>.FailureResult(deletedContainerGuard.Failure!.Code, deletedContainerGuard.Failure.Message);
            }

            directoryOperations.Move(resolvedSkillDirectory, deletedDirectory);
            if (precondition is not null)
            {
                var movedTargetResult = await precondition(deletedDirectory, cancellationToken).ConfigureAwait(false);
                if (!movedTargetResult.IsSuccess)
                {
                    directoryOperations.Move(deletedDirectory, resolvedSkillDirectory);
                    return SkillOperationResult<bool>.FailureResult(movedTargetResult.Failure!.Code, movedTargetResult.Failure.Message);
                }
            }

            DeleteDirectoryBestEffort(deletedDirectory);
            DeleteDirectoryBestEffort(transactionRoot);

            return SkillOperationResult<bool>.Success(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return SkillOperationResult<bool>.FailureResult(
                SkillFailureCodes.InstallTargetWriteFailed,
                $"Failed to delete installed SKILL package: {resolvedSkillDirectory}. {ex.Message}");
        }
        finally
        {
            if (!directoryOperations.Exists(deletedDirectory))
            {
                DeleteDirectoryBestEffort(transactionRoot);
            }
        }
    }

    /// <summary> Deletes one installed SKILL package directory without an execution precondition. </summary>
    /// <param name="targetRoot"> The resolved host target root. </param>
    /// <param name="skillDirectory"> The resolved skill package directory. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> Success when the directory is deleted or already absent; otherwise a failure. </returns>
    public ValueTask<SkillOperationResult<bool>> DeleteAsync (
        string targetRoot,
        string skillDirectory,
        CancellationToken cancellationToken = default)
    {
        return DeleteAsync(targetRoot, skillDirectory, precondition: null, cancellationToken);
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

    private void DeleteDirectoryBestEffort (string path)
    {
        if (!directoryOperations.Exists(path))
        {
            return;
        }

        try
        {
            directoryOperations.Delete(path, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
