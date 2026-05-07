using System.IO.Compression;
using MackySoft.Tests;
using MackySoft.Ucli.Skills.Distribution;
using MackySoft.Ucli.Skills.Hosts.OpenAi;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Tests.Distribution;

public sealed class SkillExportServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task ExportAsync_RejectsUnsafePackageName ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "export-unsafe-package");
        var generatedPackage = (await SkillTestData.GenerateOfficialPackagesAsync()).First();
        var package = generatedPackage with
        {
            Manifest = generatedPackage.Manifest with
            {
                SkillName = "../escape",
            },
        };
        var service = SkillTestData.CreateExportService();

        var result = await service.ExportAsync([package], OpenAiSkillHostAdapter.HostKey, scope.FullPath, CancellationToken.None);

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
        var symlinkPath = Path.Combine(outputScope.FullPath, packages[0].Manifest.SkillName);
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

        var service = SkillTestData.CreateExportService();

        var result = await service.ExportAsync(packages, OpenAiSkillHostAdapter.HostKey, outputScope.FullPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.PathUnsafe, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ExportAsync_ZipFormat_WritesDeterministicEntries ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "export-zip-deterministic");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var service = SkillTestData.CreateExportService();
        var firstZip = scope.GetPath("first.zip");
        var secondZip = scope.GetPath("second.zip");

        var first = await service.ExportAsync(packages, OpenAiSkillHostAdapter.HostKey, firstZip, SkillExportFormat.Zip, CancellationToken.None);
        var second = await service.ExportAsync(packages, OpenAiSkillHostAdapter.HostKey, secondZip, SkillExportFormat.Zip, CancellationToken.None);

        Assert.True(first.IsSuccess, first.Failure?.Message);
        Assert.True(second.IsSuccess, second.Failure?.Message);
        Assert.Equal(await File.ReadAllBytesAsync(firstZip), await File.ReadAllBytesAsync(secondZip));
        using var archive = ZipFile.OpenRead(firstZip);
        var entryNames = archive.Entries.Select(static entry => entry.FullName).ToArray();
        Assert.Equal(entryNames.Order(StringComparer.Ordinal).ToArray(), entryNames);
        Assert.Contains($"{packages[0].Manifest.SkillName}/SKILL.md", entryNames);
        Assert.Contains($"{packages[0].Manifest.SkillName}/agents/openai.yaml", entryNames);
        Assert.All(archive.Entries, static entry => Assert.Equal(new DateTime(1980, 1, 1, 0, 0, 0), entry.LastWriteTime.DateTime));
    }
}
