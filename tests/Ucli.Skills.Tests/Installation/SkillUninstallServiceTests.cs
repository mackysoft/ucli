using MackySoft.Tests;
using MackySoft.Ucli.Skills.Hosts.Claude;
using MackySoft.Ucli.Skills.Hosts.OpenAi;
using MackySoft.Ucli.Skills.Installation;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Tests.Installation;

public sealed class SkillUninstallServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task UninstallAsync_DeletesManagedOfficialSkillsAndPreservesTargetRoot ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "uninstall-delete");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var uninstallService = SkillTestData.CreateUninstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var unmanagedPath = scope.WriteFile(Path.Combine(".agents", "skills", "custom-skill", "SKILL.md"), "# Custom\n");

        var result = await uninstallService.UninstallAsync(packages, request, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.All(result.Value!.Actions, static action => Assert.Equal(SkillUninstallActionKind.Deleted, action.ActionKind));
        Assert.True(Directory.Exists(result.Value.TargetRoot));
        Assert.True(File.Exists(unmanagedPath));
        foreach (var package in packages)
        {
            Assert.False(Directory.Exists(Path.Combine(result.Value.TargetRoot, package.SkillName)), package.SkillName);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UninstallAsync_NoOps_WhenTargetRootIsMissing ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "uninstall-missing-target");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var service = SkillTestData.CreateUninstallService();

        var result = await service.UninstallAsync(
            packages,
            new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.All(result.Value!.Actions, static action => Assert.Equal(SkillUninstallActionKind.NoOp, action.ActionKind));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UninstallAsync_NoOps_WhenOfficialSkillDirectoryIsMissing ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "uninstall-missing-skill");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var uninstallService = SkillTestData.CreateUninstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        Directory.Delete(Path.Combine(install.Value!.TargetRoot, packages[0].SkillName), recursive: true);

        var result = await uninstallService.UninstallAsync([packages[0]], request, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(SkillUninstallActionKind.NoOp, result.Value!.Actions.Single().ActionKind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UninstallAsync_SkipsUnmanagedSkillDirectory ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "uninstall-unmanaged");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var service = SkillTestData.CreateUninstallService();
        var unmanagedPath = scope.WriteFile(Path.Combine(".agents", "skills", packages[0].SkillName, "SKILL.md"), "# Existing\n");

        var result = await service.UninstallAsync(
            [packages[0]],
            new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(SkillUninstallActionKind.SkippedUnmanaged, result.Value!.Actions.Single().ActionKind);
        Assert.True(File.Exists(unmanagedPath));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UninstallAsync_RejectsLocalModification ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "uninstall-local-modification");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var uninstallService = SkillTestData.CreateUninstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        File.AppendAllText(Path.Combine(install.Value!.TargetRoot, packages[0].SkillName, "SKILL.md"), "\nInjected instruction.\n");

        var result = await uninstallService.UninstallAsync(packages, request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetDigestMismatch, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UninstallAsync_RejectsSharedTargetRootFromDifferentHost ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "uninstall-host-conflict");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var uninstallService = SkillTestData.CreateUninstallService();
        var install = await installService.InstallAsync(
            packages,
            new SkillInstallRequest(ClaudeSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath, "shared-skills"),
            CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);

        var result = await uninstallService.UninstallAsync(
            packages,
            new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath, "shared-skills"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetHostConflict, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UninstallAsync_DoesNotModifyOtherHostTarget ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "uninstall-other-host");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var uninstallService = SkillTestData.CreateUninstallService();
        var openAi = await installService.InstallAsync(
            packages,
            new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath),
            CancellationToken.None);
        var claude = await installService.InstallAsync(
            packages,
            new SkillInstallRequest(ClaudeSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath),
            CancellationToken.None);
        Assert.True(openAi.IsSuccess, openAi.Failure?.Message);
        Assert.True(claude.IsSuccess, claude.Failure?.Message);

        var result = await uninstallService.UninstallAsync(
            packages,
            new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        foreach (var package in packages)
        {
            Assert.False(Directory.Exists(Path.Combine(openAi.Value!.TargetRoot, package.SkillName)), package.SkillName);
            Assert.True(Directory.Exists(Path.Combine(claude.Value!.TargetRoot, package.SkillName)), package.SkillName);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UninstallAsync_RejectsManifestSymlinkThatEscapesTarget ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("ucli-skills", "uninstall-manifest-symlink");
        using var outsideScope = TestDirectories.CreateTempScope("ucli-skills", "uninstall-manifest-symlink-outside");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var targetRoot = scope.CreateDirectory(".agents/skills");
        scope.CreateDirectory(Path.Combine(".agents", "skills", packages[0].SkillName));
        var outsideManifest = outsideScope.WriteFile("ucli-skill.json", packages[0].Files.Single(static file => file.RelativePath == "ucli-skill.json").Content);
        try
        {
            File.CreateSymbolicLink(Path.Combine(targetRoot, packages[0].SkillName, "ucli-skill.json"), outsideManifest);
        }
        catch (IOException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        var service = SkillTestData.CreateUninstallService();

        var result = await service.UninstallAsync(
            [packages[0]],
            new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.PathUnsafe, result.Failure!.Code);
    }
}
