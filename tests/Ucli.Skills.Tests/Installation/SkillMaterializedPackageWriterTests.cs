using MackySoft.Tests;
using MackySoft.Ucli.Skills.Hosts.OpenAi;
using MackySoft.Ucli.Skills.Installation;
using MackySoft.Ucli.Skills.Materialization;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Tests.Installation;

public sealed class SkillMaterializedPackageWriterTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task WriteAsync_WhenStagingWriteFails_PreservesExistingTargetAndCleansTransactionDirectory ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "writer-staging-failure-preserves");
        var targetRoot = scope.CreateDirectory(".agents/skills");
        var skillDirectory = scope.CreateDirectory(Path.Combine(".agents", "skills", "sample-skill"));
        var skillPath = scope.WriteFile(Path.Combine(".agents", "skills", "sample-skill", "SKILL.md"), "# Existing\n");
        var writer = new SkillMaterializedPackageWriter();
        var package = new SkillMaterializedPackage(
            "sample-skill",
            OpenAiSkillHostAdapter.HostKey,
            [
                SkillPackageFile.Create("nested", "file"),
                SkillPackageFile.Create("nested/file.md", "nested"),
            ]);

        var result = await writer.WriteAsync(targetRoot, skillDirectory, package, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetWriteFailed, result.Failure!.Code);
        Assert.True(Directory.Exists(skillDirectory));
        Assert.Equal("# Existing\n", File.ReadAllText(skillPath));
        Assert.False(Directory.Exists(Path.Combine(targetRoot, ".ucli-skill-transactions")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WriteAsync_WhenCommitMoveFails_RestoresExistingTargetAndCleansTransactionDirectory ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "writer-commit-failure-restores");
        var targetRoot = scope.CreateDirectory(".agents/skills");
        var skillDirectory = scope.CreateDirectory(Path.Combine(".agents", "skills", "sample-skill"));
        var skillPath = scope.WriteFile(Path.Combine(".agents", "skills", "sample-skill", "SKILL.md"), "# Existing\n");
        var writer = new SkillMaterializedPackageWriter(new CommitMoveFailingDirectoryOperations());
        var package = new SkillMaterializedPackage(
            "sample-skill",
            OpenAiSkillHostAdapter.HostKey,
            [
                SkillPackageFile.Create("SKILL.md", "# New\n"),
                SkillPackageFile.Create("new.md", "# New file\n"),
            ]);

        var result = await writer.WriteAsync(targetRoot, skillDirectory, package, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetWriteFailed, result.Failure!.Code);
        Assert.True(Directory.Exists(skillDirectory));
        Assert.Equal("# Existing\n", File.ReadAllText(skillPath));
        Assert.False(File.Exists(Path.Combine(skillDirectory, "new.md")));
        Assert.False(Directory.Exists(Path.Combine(targetRoot, ".ucli-skill-transactions")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WriteAsync_WhenMovedTargetPreconditionFails_RestoresExistingTargetAndCleansTransactionDirectory ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "writer-moved-precondition-failure");
        var targetRoot = scope.CreateDirectory(".agents/skills");
        var skillDirectory = scope.CreateDirectory(Path.Combine(".agents", "skills", "sample-skill"));
        var skillPath = scope.WriteFile(Path.Combine(".agents", "skills", "sample-skill", "SKILL.md"), "# Existing\n");
        var writer = new SkillMaterializedPackageWriter();
        var package = new SkillMaterializedPackage(
            "sample-skill",
            OpenAiSkillHostAdapter.HostKey,
            [
                SkillPackageFile.Create("SKILL.md", "# New\n"),
            ]);
        var preconditionCallCount = 0;

        var result = await writer.WriteAsync(
            targetRoot,
            skillDirectory,
            package,
            SkillMaterializedPackageWriteMode.ReplaceExisting,
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

    [Fact]
    [Trait("Size", "Small")]
    public async Task WriteAsync_WithOutsideSkillDirectory_ReturnsPathUnsafeWithoutWriting ()
    {
        using var targetScope = TestDirectories.CreateTempScope("ucli-skills", "writer-target-root");
        using var outsideScope = TestDirectories.CreateTempScope("ucli-skills", "writer-outside-root");
        var writer = new SkillMaterializedPackageWriter();
        var outsideSkillDirectory = Path.Combine(outsideScope.FullPath, "skill");

        var result = await writer.WriteAsync(
            targetScope.FullPath,
            outsideSkillDirectory,
            new SkillMaterializedPackage("skill", OpenAiSkillHostAdapter.HostKey, []),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.PathUnsafe, result.Failure!.Code);
        Assert.False(Directory.Exists(outsideSkillDirectory));
        Assert.Empty(Directory.EnumerateFileSystemEntries(outsideScope.FullPath));
    }

    private sealed class CommitMoveFailingDirectoryOperations : ISkillPackageDirectoryOperations
    {
        private int moveCount;

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
            moveCount++;
            if (moveCount == 2)
            {
                throw new IOException("Injected commit move failure.");
            }

            Directory.Move(sourceDirectoryName, destinationDirectoryName);
        }

        public void Delete (
            string path,
            bool recursive)
        {
            Directory.Delete(path, recursive);
        }
    }
}
