using MackySoft.Ucli.Skills.Materialization;
using MackySoft.Ucli.Skills.Packaging;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Writes materialized SKILL packages under a resolved host target root. </summary>
public sealed class SkillMaterializedPackageWriter : ISkillMaterializedPackageWriter
{
    private readonly ISkillPackageDirectoryOperations directoryOperations;

    /// <summary> Initializes a new instance of the <see cref="SkillMaterializedPackageWriter" /> class. </summary>
    /// <param name="directoryOperations"> The directory operations used by package transactions. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="directoryOperations" /> is <see langword="null" />. </exception>
    public SkillMaterializedPackageWriter (ISkillPackageDirectoryOperations directoryOperations)
    {
        this.directoryOperations = directoryOperations ?? throw new ArgumentNullException(nameof(directoryOperations));
    }

    /// <inheritdoc />
    public async ValueTask<SkillOperationResult<bool>> WriteAsync (
        string targetRoot,
        string skillDirectory,
        SkillMaterializedPackage materializedPackage,
        SkillMaterializedPackageWriteMode writeMode,
        Func<string, CancellationToken, ValueTask<SkillOperationResult<bool>>>? precondition,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(skillDirectory);
        ArgumentNullException.ThrowIfNull(materializedPackage);
        cancellationToken.ThrowIfCancellationRequested();

        var skillDirectoryResult = SkillPackagePathBoundary.ResolveUnderRoot(targetRoot, skillDirectory);
        if (!skillDirectoryResult.IsSuccess)
        {
            return SkillOperationResult<bool>.FailureResult(skillDirectoryResult.Failure!.Code, skillDirectoryResult.Failure.Message);
        }

        var resolvedSkillDirectory = skillDirectoryResult.Value!;
        var parentDirectory = Path.GetDirectoryName(resolvedSkillDirectory);
        if (string.IsNullOrWhiteSpace(parentDirectory))
        {
            return SkillOperationResult<bool>.FailureResult(
                SkillFailureCodes.InstallTargetWriteFailed,
                $"Skill directory parent could not be resolved: {resolvedSkillDirectory}");
        }

        var transactionRootResult = SkillPackagePathBoundary.ResolveUnderRoot(
            targetRoot,
            Path.Combine(parentDirectory, ".ucli-skill-transactions"));
        if (!transactionRootResult.IsSuccess)
        {
            return SkillOperationResult<bool>.FailureResult(transactionRootResult.Failure!.Code, transactionRootResult.Failure.Message);
        }

        var transactionRoot = transactionRootResult.Value!;
        var stagingDirectoryResult = SkillPackagePathBoundary.ResolveUnderRoot(
            targetRoot,
            Path.Combine(transactionRoot, $"{Path.GetFileName(resolvedSkillDirectory)}.staging.{Guid.NewGuid():N}"));
        if (!stagingDirectoryResult.IsSuccess)
        {
            return SkillOperationResult<bool>.FailureResult(stagingDirectoryResult.Failure!.Code, stagingDirectoryResult.Failure.Message);
        }

        var backupContainerResult = SkillPackagePathBoundary.ResolveUnderRoot(
            targetRoot,
            Path.Combine(transactionRoot, $"{Path.GetFileName(resolvedSkillDirectory)}.backup.{Guid.NewGuid():N}"));
        if (!backupContainerResult.IsSuccess)
        {
            return SkillOperationResult<bool>.FailureResult(backupContainerResult.Failure!.Code, backupContainerResult.Failure.Message);
        }

        var backupDirectoryResult = SkillPackagePathBoundary.ResolveUnderRoot(
            targetRoot,
            Path.Combine(backupContainerResult.Value!, Path.GetFileName(resolvedSkillDirectory)));
        if (!backupDirectoryResult.IsSuccess)
        {
            return SkillOperationResult<bool>.FailureResult(backupDirectoryResult.Failure!.Code, backupDirectoryResult.Failure.Message);
        }

        var stagingDirectory = stagingDirectoryResult.Value!;
        var backupContainer = backupContainerResult.Value!;
        var backupDirectory = backupDirectoryResult.Value!;
        var movedExistingToBackup = false;
        var committed = false;

        try
        {
            directoryOperations.Create(transactionRoot);
            var transactionRootGuard = SkillPackageTransactionPathGuard.ValidateCreatedDirectory(targetRoot, transactionRoot);
            if (!transactionRootGuard.IsSuccess)
            {
                return SkillOperationResult<bool>.FailureResult(transactionRootGuard.Failure!.Code, transactionRootGuard.Failure.Message);
            }

            var lockResult = SkillPackageTransactionLock.Acquire(targetRoot, transactionRoot);
            if (!lockResult.IsSuccess)
            {
                return SkillOperationResult<bool>.FailureResult(lockResult.Failure!.Code, lockResult.Failure.Message);
            }

            using var transactionLock = lockResult.Value!;
            directoryOperations.Create(stagingDirectory);
            var stagingDirectoryGuard = SkillPackageTransactionPathGuard.ValidateCreatedDirectory(targetRoot, stagingDirectory);
            if (!stagingDirectoryGuard.IsSuccess)
            {
                return SkillOperationResult<bool>.FailureResult(stagingDirectoryGuard.Failure!.Code, stagingDirectoryGuard.Failure.Message);
            }

            foreach (var file in materializedPackage.Files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var finalPathResult = SkillPackagePathBoundary.ResolvePackageFilePathUnderRoot(targetRoot, resolvedSkillDirectory, file.RelativePath);
                if (!finalPathResult.IsSuccess)
                {
                    return SkillOperationResult<bool>.FailureResult(finalPathResult.Failure!.Code, finalPathResult.Failure.Message);
                }

                var stagingPathResult = SkillPackagePathBoundary.ResolvePackageFilePathUnderRoot(targetRoot, stagingDirectory, file.RelativePath);
                if (!stagingPathResult.IsSuccess)
                {
                    return SkillOperationResult<bool>.FailureResult(stagingPathResult.Failure!.Code, stagingPathResult.Failure.Message);
                }

                await SkillPackageFileWriter.WriteAllTextAtomically(stagingPathResult.Value!, file.Content, cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (precondition is not null)
            {
                var preconditionResult = await precondition(resolvedSkillDirectory, cancellationToken).ConfigureAwait(false);
                if (!preconditionResult.IsSuccess)
                {
                    return SkillOperationResult<bool>.FailureResult(preconditionResult.Failure!.Code, preconditionResult.Failure.Message);
                }
            }

            var targetExists = directoryOperations.Exists(resolvedSkillDirectory);
            if (writeMode == SkillMaterializedPackageWriteMode.CreateNew && targetExists)
            {
                return SkillOperationResult<bool>.FailureResult(
                    SkillFailureCodes.InstallTargetDigestMismatch,
                    $"Target skill directory changed after planning; refusing to write: {resolvedSkillDirectory}");
            }

            if (writeMode == SkillMaterializedPackageWriteMode.ReplaceExisting && !targetExists)
            {
                return SkillOperationResult<bool>.FailureResult(
                    SkillFailureCodes.InstallTargetDigestMismatch,
                    $"Target skill directory changed after planning; refusing to write: {resolvedSkillDirectory}");
            }

            if (targetExists)
            {
                directoryOperations.Create(backupContainer);
                var backupContainerGuard = SkillPackageTransactionPathGuard.ValidateCreatedDirectory(targetRoot, backupContainer);
                if (!backupContainerGuard.IsSuccess)
                {
                    return SkillOperationResult<bool>.FailureResult(backupContainerGuard.Failure!.Code, backupContainerGuard.Failure.Message);
                }

                directoryOperations.Move(resolvedSkillDirectory, backupDirectory);
                movedExistingToBackup = true;
                if (precondition is not null)
                {
                    var movedTargetResult = await precondition(backupDirectory, cancellationToken).ConfigureAwait(false);
                    if (!movedTargetResult.IsSuccess)
                    {
                        try
                        {
                            directoryOperations.Move(backupDirectory, resolvedSkillDirectory);
                            movedExistingToBackup = false;
                        }
                        catch (Exception restoreException) when (restoreException is IOException or UnauthorizedAccessException)
                        {
                            return SkillOperationResult<bool>.FailureResult(
                                SkillFailureCodes.InstallTargetWriteFailed,
                                $"Failed to write SKILL package atomically and restore backup: {resolvedSkillDirectory}. Backup remains at: {backupDirectory}. {restoreException.Message}");
                        }

                        return SkillOperationResult<bool>.FailureResult(movedTargetResult.Failure!.Code, movedTargetResult.Failure.Message);
                    }
                }
            }

            directoryOperations.Move(stagingDirectory, resolvedSkillDirectory);
            committed = true;
            DeleteDirectoryBestEffort(backupDirectory);

            return SkillOperationResult<bool>.Success(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (!committed && movedExistingToBackup && !directoryOperations.Exists(resolvedSkillDirectory) && directoryOperations.Exists(backupDirectory))
            {
                try
                {
                    directoryOperations.Move(backupDirectory, resolvedSkillDirectory);
                }
                catch (Exception restoreException) when (restoreException is IOException or UnauthorizedAccessException)
                {
                    return SkillOperationResult<bool>.FailureResult(
                        SkillFailureCodes.InstallTargetWriteFailed,
                        $"Failed to write SKILL package atomically and restore backup: {resolvedSkillDirectory}. Backup remains at: {backupDirectory}. {restoreException.Message}");
                }
            }

            var backupMessage = !committed && movedExistingToBackup && directoryOperations.Exists(backupDirectory)
                ? $" Backup remains at: {backupDirectory}."
                : string.Empty;
            return SkillOperationResult<bool>.FailureResult(
                SkillFailureCodes.InstallTargetWriteFailed,
                $"Failed to write SKILL package atomically: {resolvedSkillDirectory}.{backupMessage} {ex.Message}");
        }
        finally
        {
            var preserveBackup = !committed && movedExistingToBackup && directoryOperations.Exists(backupDirectory);
            DeleteDirectoryBestEffort(stagingDirectory);
            if (committed || !movedExistingToBackup)
            {
                DeleteDirectoryBestEffort(backupDirectory);
            }

            if (!preserveBackup)
            {
                DeleteDirectoryBestEffort(transactionRoot);
            }
        }
    }

    /// <summary> Writes all files for one materialized package using the legacy upsert behavior. </summary>
    /// <param name="targetRoot"> The resolved host target root. </param>
    /// <param name="skillDirectory"> The resolved skill package directory. </param>
    /// <param name="materializedPackage"> The materialized package to write. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> Success when all file paths stay under the target root; otherwise a path-safety failure. </returns>
    public ValueTask<SkillOperationResult<bool>> WriteAsync (
        string targetRoot,
        string skillDirectory,
        SkillMaterializedPackage materializedPackage,
        CancellationToken cancellationToken = default)
    {
        return WriteAsync(
            targetRoot,
            skillDirectory,
            materializedPackage,
            directoryOperations.Exists(skillDirectory) ? SkillMaterializedPackageWriteMode.ReplaceExisting : SkillMaterializedPackageWriteMode.CreateNew,
            precondition: null,
            cancellationToken);
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
