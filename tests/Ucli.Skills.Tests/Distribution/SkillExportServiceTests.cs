using MackySoft.Tests;
using MackySoft.Ucli.Skills.Distribution;
using MackySoft.Ucli.Skills.Hosts;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Tests.Distribution;

public sealed class SkillExportServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task ExportAsync_RejectsUnsafePackageName ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "export-unsafe-package");
        var package = (await SkillTestData.GenerateOfficialPackagesAsync()).First() with
        {
            SkillName = "../escape",
        };
        var service = new SkillExportService();

        var result = await service.ExportAsync([package], SkillHostKind.OpenAi, scope.FullPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.PathUnsafe, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ExportAsync_RejectsExistingSkillDirectoryThatEscapesThroughSymlink ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var outputScope = TestDirectories.CreateTempScope("ucli-skills", "export-skill-symlink");
        using var outsideScope = TestDirectories.CreateTempScope("ucli-skills", "export-skill-symlink-outside");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var symlinkPath = Path.Combine(outputScope.FullPath, packages[0].SkillName);
        try
        {
            Directory.CreateSymbolicLink(symlinkPath, outsideScope.FullPath);
        }
        catch (IOException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        var service = new SkillExportService();

        var result = await service.ExportAsync(packages, SkillHostKind.OpenAi, outputScope.FullPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.PathUnsafe, result.Failure!.Code);
    }
}
