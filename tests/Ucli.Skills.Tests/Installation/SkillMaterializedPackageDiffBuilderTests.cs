using MackySoft.Tests;
using MackySoft.Ucli.Skills.Hosts.OpenAi;
using MackySoft.Ucli.Skills.Installation;
using MackySoft.Ucli.Skills.Materialization;
using MackySoft.Ucli.Skills.Packaging;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Tests.Installation;

public sealed class SkillMaterializedPackageDiffBuilderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task BuildAsync_ReturnsAddedModifiedAndDeletedFileDiffs ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "diff-builder-kinds");
        var skillDirectory = scope.CreateDirectory("sample-skill");
        scope.WriteFile(Path.Combine("sample-skill", "SKILL.md"), "# Before\n");
        scope.WriteFile(Path.Combine("sample-skill", "obsolete.md"), "# Obsolete\n");
        var package = new SkillMaterializedPackage(
            "sample-skill",
            OpenAiSkillHostAdapter.HostKey,
            [
                SkillPackageFile.Create("SKILL.md", "# After\n"),
                SkillPackageFile.Create("new.md", "# New\n"),
            ]);
        var builder = new SkillMaterializedPackageDiffBuilder();

        var result = await builder.BuildAsync(skillDirectory, package, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var files = result.Value!.Single().Files.OrderBy(static file => file.RelativePath, StringComparer.Ordinal).ToArray();
        Assert.Collection(
            files,
            static file =>
            {
                Assert.Equal("SKILL.md", file.RelativePath);
                Assert.Equal(SkillDiffChangeKind.Modified, file.ChangeKind);
                Assert.Equal("# Before\n", file.BeforeContent);
                Assert.Equal("# After\n", file.AfterContent);
            },
            static file =>
            {
                Assert.Equal("new.md", file.RelativePath);
                Assert.Equal(SkillDiffChangeKind.Added, file.ChangeKind);
                Assert.Null(file.BeforeContent);
                Assert.Equal("# New\n", file.AfterContent);
            },
            static file =>
            {
                Assert.Equal("obsolete.md", file.RelativePath);
                Assert.Equal(SkillDiffChangeKind.Deleted, file.ChangeKind);
                Assert.Equal("# Obsolete\n", file.BeforeContent);
                Assert.Null(file.AfterContent);
            });
    }
}
