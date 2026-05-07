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

        var result = await uninstallService.UninstallAsync(new SkillUninstallInput(packages, request), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.All(result.Value!.Actions, static action => Assert.Equal(SkillUninstallActionKind.Deleted, action.ActionKind));
        Assert.True(Directory.Exists(result.Value.TargetRoot));
        Assert.True(File.Exists(unmanagedPath));
        foreach (var package in packages)
        {
            Assert.False(Directory.Exists(Path.Combine(result.Value.TargetRoot, package.Manifest.SkillName)), package.Manifest.SkillName);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UninstallAsync_DryRunReturnsDeletedPlanWithoutDeleting ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "uninstall-dry-run-delete");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var uninstallService = SkillTestData.CreateUninstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);

        var result = await uninstallService.UninstallAsync(new SkillUninstallInput(packages, request, DryRun: true), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.True(result.Value!.DryRun);
        Assert.All(result.Value.Actions, static action => Assert.Equal(SkillUninstallActionKind.Deleted, action.ActionKind));
        foreach (var package in packages)
        {
            Assert.True(Directory.Exists(Path.Combine(result.Value.TargetRoot, package.Manifest.SkillName)), package.Manifest.SkillName);
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
            new SkillUninstallInput(
                packages,
                new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath)),
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
        Directory.Delete(Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName), recursive: true);

        var result = await uninstallService.UninstallAsync(new SkillUninstallInput([packages[0]], request), CancellationToken.None);

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
        var unmanagedPath = scope.WriteFile(Path.Combine(".agents", "skills", packages[0].Manifest.SkillName, "SKILL.md"), "# Existing\n");

        var result = await service.UninstallAsync(
            new SkillUninstallInput(
                [packages[0]],
                new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath)),
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
        File.AppendAllText(Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName, "SKILL.md"), "\nInjected instruction.\n");

        var result = await uninstallService.UninstallAsync(new SkillUninstallInput(packages, request), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetDigestMismatch, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UninstallAsync_WhenLaterTargetHasLocalModification_DoesNotDeleteEarlierPackage ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "uninstall-plan-before-delete-local-modification");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var uninstallService = SkillTestData.CreateUninstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync([packages[0], packages[1]], request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var firstSkillDirectory = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName);
        var modifiedSkillDirectory = Path.Combine(install.Value.TargetRoot, packages[1].Manifest.SkillName);
        File.AppendAllText(Path.Combine(modifiedSkillDirectory, "SKILL.md"), "\nInjected instruction.\n");

        var result = await uninstallService.UninstallAsync(new SkillUninstallInput([packages[0], packages[1]], request), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetDigestMismatch, result.Failure!.Code);
        Assert.True(Directory.Exists(firstSkillDirectory));
        Assert.True(Directory.Exists(modifiedSkillDirectory));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UninstallAsync_WhenTargetChangesAfterPlanning_ReturnsFailureWithoutDeleting ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "uninstall-target-race");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var uninstallService = SkillTestData.CreateUninstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync([packages[0], packages[1]], request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var skillDirectory = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName);
        var skillPath = Path.Combine(skillDirectory, "SKILL.md");
        var secondPackage = SkillTestData.WithFileEnumerationCallback(packages[1], () =>
            File.AppendAllText(skillPath, "\nInjected after planning.\n"));

        var result = await uninstallService.UninstallAsync(new SkillUninstallInput([packages[0], secondPackage], request), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetDigestMismatch, result.Failure!.Code);
        Assert.True(Directory.Exists(skillDirectory));
        Assert.Contains("Injected after planning.", File.ReadAllText(skillPath), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UninstallAsync_DryRunBlocksLocalModificationWithoutDeleting ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "uninstall-dry-run-local-modification");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var uninstallService = SkillTestData.CreateUninstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var skillDirectory = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName);
        File.AppendAllText(Path.Combine(skillDirectory, "SKILL.md"), "\nInjected instruction.\n");

        var result = await uninstallService.UninstallAsync(new SkillUninstallInput(packages, request, DryRun: true), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = result.Value!.Actions.Single(action => action.Identity.SkillName == packages[0].Manifest.SkillName);
        Assert.Equal(SkillUninstallActionKind.BlockedLocalModification, action.ActionKind);
        Assert.Equal(SkillBlockedReason.LocalModificationRequiresForce, action.BlockedReason);
        Assert.True(Directory.Exists(skillDirectory));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UninstallAsync_WithForceDeletesLocalModification ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "uninstall-force-local-modification");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var uninstallService = SkillTestData.CreateUninstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var skillDirectory = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName);
        File.AppendAllText(Path.Combine(skillDirectory, "SKILL.md"), "\nInjected instruction.\n");

        var result = await uninstallService.UninstallAsync(new SkillUninstallInput([packages[0]], request, Force: true), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(SkillUninstallActionKind.Deleted, result.Value!.Actions.Single().ActionKind);
        Assert.False(Directory.Exists(skillDirectory));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UninstallAsync_RejectsLocalEmptyDirectory ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "uninstall-local-empty-directory");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var uninstallService = SkillTestData.CreateUninstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var localDirectory = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName, "local-notes");
        Directory.CreateDirectory(localDirectory);

        var result = await uninstallService.UninstallAsync(new SkillUninstallInput(packages, request), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetDigestMismatch, result.Failure!.Code);
        Assert.True(Directory.Exists(localDirectory));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UninstallAsync_RejectsLocalDirectorySymlink ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("ucli-skills", "uninstall-local-directory-symlink");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var uninstallService = SkillTestData.CreateUninstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var skillDirectory = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName);
        var allowedDirectory = Path.Combine(skillDirectory, "agents");
        Assert.True(Directory.Exists(allowedDirectory));
        var localDirectoryLink = Path.Combine(skillDirectory, "local-agents");
        try
        {
            Directory.CreateSymbolicLink(localDirectoryLink, allowedDirectory);
        }
        catch (IOException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        var result = await uninstallService.UninstallAsync(new SkillUninstallInput(packages, request), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetDigestMismatch, result.Failure!.Code);
        Assert.True(Directory.Exists(localDirectoryLink));
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
            new SkillUninstallInput(
                packages,
                new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath, "shared-skills")),
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
            new SkillUninstallInput(
                packages,
                new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath)),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        foreach (var package in packages)
        {
            Assert.False(Directory.Exists(Path.Combine(openAi.Value!.TargetRoot, package.Manifest.SkillName)), package.Manifest.SkillName);
            Assert.True(Directory.Exists(Path.Combine(claude.Value!.TargetRoot, package.Manifest.SkillName)), package.Manifest.SkillName);
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
        scope.CreateDirectory(Path.Combine(".agents", "skills", packages[0].Manifest.SkillName));
        var outsideManifest = outsideScope.WriteFile("ucli-skill.json", packages[0].Files.Single(static file => file.RelativePath == "ucli-skill.json").Content);
        try
        {
            File.CreateSymbolicLink(Path.Combine(targetRoot, packages[0].Manifest.SkillName, "ucli-skill.json"), outsideManifest);
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
            new SkillUninstallInput(
                [packages[0]],
                new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath)),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.PathUnsafe, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task PackageRemoverDeleteAsync_RejectsTargetRootDeletion ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "uninstall-remover-target-root");
        var remover = SkillTestData.CreatePackageRemover();

        var result = await remover.DeleteAsync(scope.FullPath, scope.FullPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.PathUnsafe, result.Failure!.Code);
        Assert.True(Directory.Exists(scope.FullPath));
    }
}
