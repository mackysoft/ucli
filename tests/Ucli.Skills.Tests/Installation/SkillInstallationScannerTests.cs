using MackySoft.Tests;
using MackySoft.Ucli.Skills.Generation;
using MackySoft.Ucli.Skills.Hosts;
using MackySoft.Ucli.Skills.Installation;
using MackySoft.Ucli.Skills.Shared;

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

        var scanResult = await scanner.ScanAsync(packages, installResult.Value!.TargetRoot, SkillHostKind.OpenAi, CancellationToken.None);

        Assert.True(scanResult.IsSuccess, scanResult.Failure?.Message);
        Assert.Equal(SkillTestData.ExpectedSkillNames, scanResult.Value!.Select(static skill => skill.Identity.SkillName).Order(StringComparer.Ordinal).ToArray());
        Assert.All(scanResult.Value!, skill =>
        {
            Assert.Equal(SkillHostKind.OpenAi, skill.Identity.Host);
            Assert.Equal(installResult.Value.TargetRoot, skill.Identity.TargetRoot);
        });
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ScanAsync_ReturnsUnsupportedHostFailure_WhenHostIsUnknown ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "scan-unsupported-host");
        var scanner = new SkillInstallationScanner();

        var result = await scanner.ScanAsync(Array.Empty<CanonicalSkillPackage>(), scope.FullPath, (SkillHostKind)999, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.HostUnsupported, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ScanAsync_RejectsInvalidManifestWithoutThrowing ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "scan-invalid-manifest");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var targetRoot = scope.CreateDirectory(".agents/skills");
        scope.WriteFile(".agents/skills/sample-skill/ucli-skill.json", "{}");
        var scanner = new SkillInstallationScanner();

        var result = await scanner.ScanAsync(packages, targetRoot, SkillHostKind.OpenAi, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.ManifestInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ScanAsync_RejectsManifestWhoseSkillNameDoesNotMatchDirectory ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "scan-directory-mismatch");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var manifest = packages[0].Files.Single(static file => file.RelativePath == "ucli-skill.json").Content;
        var targetRoot = scope.CreateDirectory(".agents/skills");
        scope.WriteFile(".agents/skills/not-the-skill/ucli-skill.json", manifest);
        var scanner = new SkillInstallationScanner();

        var result = await scanner.ScanAsync(packages, targetRoot, SkillHostKind.OpenAi, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.ManifestInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ScanAsync_RejectsSkillMaterializedForDifferentHost ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "scan-host-conflict");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var installService = new SkillInstallService();
        var installResult = await installService.InstallAsync(
            packages,
            new SkillInstallRequest(SkillHostKind.Claude, SkillScopeKind.Project, scope.FullPath, "shared-skills"),
            CancellationToken.None);
        Assert.True(installResult.IsSuccess, installResult.Failure?.Message);
        var scanner = new SkillInstallationScanner();

        var scanResult = await scanner.ScanAsync(packages, installResult.Value!.TargetRoot, SkillHostKind.OpenAi, CancellationToken.None);

        Assert.False(scanResult.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetHostConflict, scanResult.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ScanAsync_RejectsModifiedInstalledSkillBody ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "scan-body-drift");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var installService = new SkillInstallService();
        var installResult = await installService.InstallAsync(
            packages,
            new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath),
            CancellationToken.None);
        Assert.True(installResult.IsSuccess, installResult.Failure?.Message);
        File.AppendAllText(Path.Combine(installResult.Value!.TargetRoot, packages[0].SkillName, "SKILL.md"), "\nInjected instruction.\n");
        var scanner = new SkillInstallationScanner();

        var scanResult = await scanner.ScanAsync(packages, installResult.Value.TargetRoot, SkillHostKind.OpenAi, CancellationToken.None);

        Assert.False(scanResult.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetDigestMismatch, scanResult.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ScanAsync_RejectsUnexpectedInstalledFile ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "scan-extra-file");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var installService = new SkillInstallService();
        var installResult = await installService.InstallAsync(
            packages,
            new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath),
            CancellationToken.None);
        Assert.True(installResult.IsSuccess, installResult.Failure?.Message);
        File.WriteAllText(Path.Combine(installResult.Value!.TargetRoot, packages[0].SkillName, "references", "extra.md"), "# Extra\n");
        var scanner = new SkillInstallationScanner();

        var scanResult = await scanner.ScanAsync(packages, installResult.Value.TargetRoot, SkillHostKind.OpenAi, CancellationToken.None);

        Assert.False(scanResult.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetDigestMismatch, scanResult.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ScanAsync_IgnoresNestedStrayManifestOutsideSkillDirectories ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "scan-nested-stray");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var installService = new SkillInstallService();
        var installResult = await installService.InstallAsync(
            packages,
            new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath),
            CancellationToken.None);
        Assert.True(installResult.IsSuccess, installResult.Failure?.Message);
        scope.WriteFile(Path.Combine(".agents", "skills", "unmanaged", "nested", "ucli-skill.json"), "{}");
        var scanner = new SkillInstallationScanner();

        var scanResult = await scanner.ScanAsync(packages, installResult.Value!.TargetRoot, SkillHostKind.OpenAi, CancellationToken.None);

        Assert.True(scanResult.IsSuccess, scanResult.Failure?.Message);
        Assert.Equal(SkillTestData.ExpectedSkillNames.Length, scanResult.Value!.Count);
    }
}
