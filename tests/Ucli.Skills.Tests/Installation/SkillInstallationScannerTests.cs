using MackySoft.Tests;
using MackySoft.Ucli.Skills.Hosts;
using MackySoft.Ucli.Skills.Installation;

namespace MackySoft.Ucli.Skills.Tests.Installation;

public sealed class SkillInstallationScannerTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task ScanAsync_ReadsInstalledManifestsFromTargetRoot ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "scan-installed");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var installService = new SkillInstallService();
        var installResult = await installService.InstallAsync(
            packages,
            new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath),
            CancellationToken.None);
        Assert.True(installResult.IsSuccess, installResult.Failure?.Message);
        var scanner = new SkillInstallationScanner();

        var scanResult = await scanner.ScanAsync(installResult.Value!.TargetRoot, SkillHostKind.OpenAi, CancellationToken.None);

        Assert.True(scanResult.IsSuccess, scanResult.Failure?.Message);
        Assert.Equal(SkillTestData.ExpectedSkillNames, scanResult.Value!.Select(static skill => skill.Identity.SkillName).Order(StringComparer.Ordinal).ToArray());
        Assert.All(scanResult.Value!, skill =>
        {
            Assert.Equal(SkillHostKind.OpenAi, skill.Identity.Host);
            Assert.Equal(installResult.Value.TargetRoot, skill.Identity.TargetRoot);
        });
    }
}
