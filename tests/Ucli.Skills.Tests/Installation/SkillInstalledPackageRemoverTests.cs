using MackySoft.Tests;
using MackySoft.Ucli.Skills.Installation;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Tests.Installation;

public sealed class SkillInstalledPackageRemoverTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task DeleteAsync_WhenMovedDirectoryCleanupFails_CommitsDeletionWithoutRestoringPartialTree ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "remover-delete-failure-restores");
        var targetRoot = scope.CreateDirectory(".agents/skills");
        var skillDirectory = scope.CreateDirectory(Path.Combine(".agents", "skills", "sample-skill"));
        scope.WriteFile(Path.Combine(".agents", "skills", "sample-skill", "SKILL.md"), "# Existing\n");
        scope.WriteFile(Path.Combine(".agents", "skills", "sample-skill", "nested", "file.md"), "# Nested\n");
        var remover = new SkillInstalledPackageRemover(new DeleteMovedDirectoryFailingOperations());

        var result = await remover.DeleteAsync(targetRoot, skillDirectory, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.False(Directory.Exists(skillDirectory));
        var transactionRoot = Path.Combine(targetRoot, ".ucli-skill-transactions");
        Assert.True(Directory.Exists(transactionRoot));
        Assert.Contains(Directory.EnumerateDirectories(transactionRoot), static path => path.Contains(".delete.", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DeleteAsync_WhenMovedTargetPreconditionFails_RestoresTargetAndCleansTransactionDirectory ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "remover-moved-precondition-failure");
        var targetRoot = scope.CreateDirectory(".agents/skills");
        var skillDirectory = scope.CreateDirectory(Path.Combine(".agents", "skills", "sample-skill"));
        var skillPath = scope.WriteFile(Path.Combine(".agents", "skills", "sample-skill", "SKILL.md"), "# Existing\n");
        var remover = new SkillInstalledPackageRemover();
        var preconditionCallCount = 0;

        var result = await remover.DeleteAsync(
            targetRoot,
            skillDirectory,
            (_, _) => ValueTask.FromResult(
                ++preconditionCallCount == 1
                    ? SkillOperationResult<bool>.Success(true)
                    : SkillOperationResult<bool>.FailureResult(SkillFailureCodes.InstallTargetDigestMismatch, "Synthetic moved target failure.")),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetDigestMismatch, result.Failure!.Code);
        Assert.True(Directory.Exists(skillDirectory));
        Assert.Equal("# Existing\n", File.ReadAllText(skillPath));
        Assert.False(Directory.Exists(Path.Combine(targetRoot, ".ucli-skill-transactions")));
    }

    private sealed class DeleteMovedDirectoryFailingOperations : ISkillPackageDirectoryOperations
    {
        public bool Exists (string path)
        {
            return Directory.Exists(path);
        }

        public void Create (string path)
        {
            Directory.CreateDirectory(path);
        }

        public void Move (
            string sourceDirectoryName,
            string destinationDirectoryName)
        {
            Directory.Move(sourceDirectoryName, destinationDirectoryName);
        }

        public void Delete (
            string path,
            bool recursive)
        {
            if (path.Contains(".delete.", StringComparison.Ordinal))
            {
                File.Delete(Path.Combine(path, "SKILL.md"));
                throw new IOException("Injected moved directory delete failure.");
            }

            if (path.Contains(".ucli-skill-transactions", StringComparison.Ordinal))
            {
                throw new IOException("Injected transaction cleanup failure.");
            }

            Directory.Delete(path, recursive);
        }
    }
}
