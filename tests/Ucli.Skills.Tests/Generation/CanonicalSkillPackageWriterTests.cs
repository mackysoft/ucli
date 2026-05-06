using MackySoft.Tests;
using MackySoft.Ucli.Skills.Generation;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Tests.Generation;

public sealed class CanonicalSkillPackageWriterTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task WriteAllAsync_AcceptsSkillsOutputRootWithTrailingSeparator ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "writer-trailing-separator");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var outputRoot = scope.GetPath("skills") + Path.DirectorySeparatorChar;
        var writer = new CanonicalSkillPackageWriter();

        var result = await writer.WriteAllAsync(packages, outputRoot, cleanOutputRoot: true, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.True(Directory.Exists(Path.Combine(result.Value!, packages[0].Manifest.SkillName)));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WriteAllAsync_RejectsEmptyPackageSet ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "writer-empty");
        var outputRoot = scope.CreateDirectory("skills");
        var existingFile = scope.WriteFile("skills/existing.txt", "existing");
        var writer = new CanonicalSkillPackageWriter();

        var result = await writer.WriteAllAsync([], outputRoot, cleanOutputRoot: true, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.ManifestInvalid, result.Failure!.Code);
        Assert.True(File.Exists(existingFile));
    }
}
