using MackySoft.Ucli.Skills.Materialization;
using MackySoft.Ucli.Skills.Packaging;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Writes materialized SKILL packages under a resolved host target root. </summary>
public sealed class SkillMaterializedPackageWriter : ISkillMaterializedPackageWriter
{
    /// <summary> Writes all files for one materialized package. </summary>
    /// <param name="targetRoot"> The resolved host target root. </param>
    /// <param name="skillDirectory"> The resolved skill package directory. </param>
    /// <param name="materializedPackage"> The materialized package to write. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> Success when all file paths stay under the target root; otherwise a path-safety failure. </returns>
    public async ValueTask<SkillOperationResult<bool>> WriteAsync (
        string targetRoot,
        string skillDirectory,
        SkillMaterializedPackage materializedPackage,
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

        var backupDirectoryResult = SkillPackagePathBoundary.ResolveUnderRoot(
            targetRoot,
            Path.Combine(transactionRoot, $"{Path.GetFileName(resolvedSkillDirectory)}.backup.{Guid.NewGuid():N}"));
        if (!backupDirectoryResult.IsSuccess)
        {
            return SkillOperationResult<bool>.FailureResult(backupDirectoryResult.Failure!.Code, backupDirectoryResult.Failure.Message);
        }

        var stagingDirectory = stagingDirectoryResult.Value!;
        var backupDirectory = backupDirectoryResult.Value!;
        var movedExistingToBackup = false;
        var committed = false;

        try
        {
            Directory.CreateDirectory(transactionRoot);
            Directory.CreateDirectory(stagingDirectory);
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
            if (Directory.Exists(resolvedSkillDirectory))
            {
                Directory.Move(resolvedSkillDirectory, backupDirectory);
                movedExistingToBackup = true;
            }

            Directory.Move(stagingDirectory, resolvedSkillDirectory);
            committed = true;
            DeleteDirectoryBestEffort(backupDirectory);

            return SkillOperationResult<bool>.Success(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (!committed && movedExistingToBackup && !Directory.Exists(resolvedSkillDirectory) && Directory.Exists(backupDirectory))
            {
                try
                {
                    Directory.Move(backupDirectory, resolvedSkillDirectory);
                }
                catch (Exception restoreException) when (restoreException is IOException or UnauthorizedAccessException)
                {
                    return SkillOperationResult<bool>.FailureResult(
                        SkillFailureCodes.InstallTargetWriteFailed,
                        $"Failed to write SKILL package atomically and restore backup: {resolvedSkillDirectory}. Backup remains at: {backupDirectory}. {restoreException.Message}");
                }
            }

            var backupMessage = !committed && movedExistingToBackup && Directory.Exists(backupDirectory)
                ? $" Backup remains at: {backupDirectory}."
                : string.Empty;
            return SkillOperationResult<bool>.FailureResult(
                SkillFailureCodes.InstallTargetWriteFailed,
                $"Failed to write SKILL package atomically: {resolvedSkillDirectory}.{backupMessage} {ex.Message}");
        }
        finally
        {
            var preserveBackup = !committed && movedExistingToBackup && Directory.Exists(backupDirectory);
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

    private static void DeleteDirectoryBestEffort (string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
