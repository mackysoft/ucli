using MackySoft.Tests;
using MackySoft.Ucli.Skills.Hosts.Claude;
using MackySoft.Ucli.Skills.Hosts.OpenAi;
using MackySoft.Ucli.Skills.Installation;
using MackySoft.Ucli.Skills.Materialization;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Tests.Installation;

public sealed class SkillUpdateServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_CreatesThenNoOps_WhenTargetIsCurrent ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "update-create-noop");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var service = SkillTestData.CreateUpdateService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);

        var created = await service.UpdateAsync(new SkillUpdateInput(packages, request), CancellationToken.None);
        var noOp = await service.UpdateAsync(new SkillUpdateInput(packages, request), CancellationToken.None);

        Assert.True(created.IsSuccess, created.Failure?.Message);
        Assert.True(noOp.IsSuccess, noOp.Failure?.Message);
        Assert.All(created.Value!.Actions, static action => Assert.Equal(SkillUpdateActionKind.Created, action.ActionKind));
        Assert.All(noOp.Value!.Actions, static action => Assert.Equal(SkillUpdateActionKind.NoOp, action.ActionKind));
        foreach (var package in packages)
        {
            Assert.True(File.Exists(Path.Combine(created.Value.TargetRoot, package.Manifest.SkillName, "SKILL.md")), package.Manifest.SkillName);
            Assert.True(File.Exists(Path.Combine(created.Value.TargetRoot, package.Manifest.SkillName, "ucli-skill.json")), package.Manifest.SkillName);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_UpdatesCleanOutdatedPackage ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "update-outdated");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var updatedPackages = SkillTestData.ReplacePackage(packages, SkillTestData.CreatePackageWithUpdatedBody(packages[0]));

        var result = await updateService.UpdateAsync(new SkillUpdateInput(updatedPackages, request), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(SkillUpdateActionKind.Updated, result.Value!.Actions.Single(action => action.Identity.SkillName == packages[0].Manifest.SkillName).ActionKind);
        Assert.All(result.Value.Actions.Where(action => action.Identity.SkillName != packages[0].Manifest.SkillName), static action =>
            Assert.Equal(SkillUpdateActionKind.NoOp, action.ActionKind));

        var expectedManifest = updatedPackages[0].Files.Single(static file => file.RelativePath == "ucli-skill.json").Content;
        var actualManifest = File.ReadAllText(Path.Combine(result.Value.TargetRoot, packages[0].Manifest.SkillName, "ucli-skill.json"));
        Assert.Equal(expectedManifest, actualManifest);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_RejectsExistingUnmanagedTarget ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "update-unmanaged");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var service = SkillTestData.CreateUpdateService();
        scope.WriteFile(Path.Combine(".agents", "skills", packages[0].Manifest.SkillName, "SKILL.md"), "# Existing\n");

        var result = await service.UpdateAsync(
            new SkillUpdateInput(
                packages,
                new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath)),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetUnmanaged, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_RejectsLocalModification ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "update-local-modification");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        File.AppendAllText(Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName, "SKILL.md"), "\nInjected instruction.\n");

        var result = await updateService.UpdateAsync(new SkillUpdateInput(packages, request), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetDigestMismatch, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_DryRunCreatesPlanWithoutWritingFiles ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "update-dry-run-create");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var service = SkillTestData.CreateUpdateService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);

        var result = await service.UpdateAsync(new SkillUpdateInput([packages[0]], request, DryRun: true, PrintDiff: true), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = result.Value!.Actions.Single();
        Assert.Equal(SkillUpdateActionKind.Created, action.ActionKind);
        Assert.NotEmpty(action.Diffs!);
        Assert.False(Directory.Exists(Path.Combine(result.Value.TargetRoot, packages[0].Manifest.SkillName)));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_DryRunUpdatesCleanOutdatedPlanWithoutWriting ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "update-dry-run-outdated");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync([packages[0]], request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var manifestPath = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName, "ucli-skill.json");
        var originalManifest = File.ReadAllText(manifestPath);
        var updatedPackage = SkillTestData.CreatePackageWithUpdatedBody(packages[0]);

        var result = await updateService.UpdateAsync(new SkillUpdateInput([updatedPackage], request, DryRun: true, PrintDiff: true), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = result.Value!.Actions.Single();
        Assert.Equal(SkillUpdateActionKind.Updated, action.ActionKind);
        Assert.NotEmpty(action.Diffs!);
        Assert.Equal(originalManifest, File.ReadAllText(manifestPath));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_DryRunBlocksLocalModificationWithoutWriting ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "update-dry-run-local-modification");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var skillPath = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName, "SKILL.md");
        File.AppendAllText(skillPath, "\nInjected instruction.\n");
        var modifiedSkill = File.ReadAllText(skillPath);

        var result = await updateService.UpdateAsync(new SkillUpdateInput(packages, request, DryRun: true, PrintDiff: true), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = result.Value!.Actions.Single(action => action.Identity.SkillName == packages[0].Manifest.SkillName);
        Assert.Equal(SkillUpdateActionKind.BlockedLocalModification, action.ActionKind);
        Assert.Equal(SkillBlockedReason.LocalModificationRequiresForce, action.BlockedReason);
        Assert.NotEmpty(action.Diffs!);
        Assert.Equal(modifiedSkill, File.ReadAllText(skillPath));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_WithForceOverwritesLocalModification ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "update-force-local-modification");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var skillPath = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName, "SKILL.md");
        File.AppendAllText(skillPath, "\nInjected instruction.\n");

        var result = await updateService.UpdateAsync(new SkillUpdateInput(packages, request, Force: true, PrintDiff: true), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = result.Value!.Actions.Single(action => action.Identity.SkillName == packages[0].Manifest.SkillName);
        Assert.Equal(SkillUpdateActionKind.Updated, action.ActionKind);
        Assert.NotEmpty(action.Diffs!);
        Assert.DoesNotContain("Injected instruction.", File.ReadAllText(skillPath), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_DryRunBlocksUnmanagedTargetEvenWithForce ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "update-dry-run-unmanaged-force");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var service = SkillTestData.CreateUpdateService();
        var unmanagedPath = scope.WriteFile(Path.Combine(".agents", "skills", packages[0].Manifest.SkillName, "SKILL.md"), "# Existing\n");

        var result = await service.UpdateAsync(
            new SkillUpdateInput(
                [packages[0]],
                new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath),
                DryRun: true,
                Force: true,
                PrintDiff: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = result.Value!.Actions.Single();
        Assert.Equal(SkillUpdateActionKind.BlockedUnmanaged, action.ActionKind);
        Assert.Equal(SkillBlockedReason.UnmanagedTarget, action.BlockedReason);
        Assert.Empty(action.Diffs!);
        Assert.True(File.Exists(unmanagedPath));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_WhenWriterFails_ReturnsWriteFailure ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "update-writer-failure");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService(new FailingPackageWriter());
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var updatedPackages = SkillTestData.ReplacePackage(packages, SkillTestData.CreatePackageWithUpdatedBody(packages[0]));

        var result = await updateService.UpdateAsync(new SkillUpdateInput(updatedPackages, request), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetWriteFailed, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_WhenLaterTargetIsUnmanaged_DoesNotUpdateEarlierPackage ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "update-plan-before-write-unmanaged");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync([packages[0]], request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var manifestPath = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName, "ucli-skill.json");
        var originalManifest = File.ReadAllText(manifestPath);
        var unmanagedPath = scope.WriteFile(Path.Combine(".agents", "skills", packages[1].Manifest.SkillName, "SKILL.md"), "# Existing\n");
        var updatedPackage = SkillTestData.CreatePackageWithUpdatedBody(packages[0]);

        var result = await updateService.UpdateAsync(new SkillUpdateInput([updatedPackage, packages[1]], request), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetUnmanaged, result.Failure!.Code);
        Assert.Equal(originalManifest, File.ReadAllText(manifestPath));
        Assert.True(File.Exists(unmanagedPath));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_WhenTargetChangesAfterPlanning_ReturnsFailureWithoutOverwriting ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "update-target-race");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync([packages[0], packages[1]], request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var skillPath = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName, "SKILL.md");
        var updatedPackage = SkillTestData.CreatePackageWithUpdatedBody(packages[0]);
        var secondPackage = SkillTestData.WithFileEnumerationCallback(packages[1], () =>
            File.AppendAllText(skillPath, "\nInjected after planning.\n"));

        var result = await updateService.UpdateAsync(new SkillUpdateInput([updatedPackage, secondPackage], request), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetDigestMismatch, result.Failure!.Code);
        var skillText = File.ReadAllText(skillPath);
        Assert.Contains("Injected after planning.", skillText, StringComparison.Ordinal);
        Assert.DoesNotContain("Official update.", skillText, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_WhenLaterTargetChangesAfterPlanning_ReturnsFailureWithoutUpdatingEarlierTarget ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "update-later-target-race");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync([packages[0], packages[1]], request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var firstSkillPath = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName, "SKILL.md");
        var secondSkillPath = Path.Combine(install.Value.TargetRoot, packages[1].Manifest.SkillName, "SKILL.md");
        var firstUpdatedPackage = SkillTestData.CreatePackageWithUpdatedBody(packages[0]);
        var secondUpdatedPackage = SkillTestData.WithFileEnumerationCallback(
            SkillTestData.CreatePackageWithUpdatedBody(packages[1]),
            () => File.AppendAllText(secondSkillPath, "\nInjected after planning.\n"));

        var result = await updateService.UpdateAsync(new SkillUpdateInput([firstUpdatedPackage, secondUpdatedPackage], request), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetDigestMismatch, result.Failure!.Code);
        Assert.DoesNotContain("Official update.", File.ReadAllText(firstSkillPath), StringComparison.Ordinal);
        Assert.Contains("Injected after planning.", File.ReadAllText(secondSkillPath), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_RejectsLocalEmptyDirectoryBeforeReplacingOutdatedPackage ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "update-local-empty-directory");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var localDirectory = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName, "local-notes");
        Directory.CreateDirectory(localDirectory);
        var updatedPackages = SkillTestData.ReplacePackage(packages, SkillTestData.CreatePackageWithUpdatedBody(packages[0]));

        var result = await updateService.UpdateAsync(new SkillUpdateInput(updatedPackages, request), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetDigestMismatch, result.Failure!.Code);
        Assert.True(Directory.Exists(localDirectory));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_RejectsLocalDirectorySymlinkBeforeReplacingOutdatedPackage ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("ucli-skills", "update-local-directory-symlink");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
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

        var updatedPackages = SkillTestData.ReplacePackage(packages, SkillTestData.CreatePackageWithUpdatedBody(packages[0]));

        var result = await updateService.UpdateAsync(new SkillUpdateInput(updatedPackages, request), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetDigestMismatch, result.Failure!.Code);
        Assert.True(Directory.Exists(localDirectoryLink));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_RejectsSharedTargetRootFromDifferentHost ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "update-host-conflict");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var install = await installService.InstallAsync(
            packages,
            new SkillInstallRequest(ClaudeSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath, "shared-skills"),
            CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);

        var result = await updateService.UpdateAsync(
            new SkillUpdateInput(
                packages,
                new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath, "shared-skills")),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetHostConflict, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_DoesNotModifyOtherHostTarget ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "update-other-host");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var openAiRequest = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var claudeRequest = new SkillInstallRequest(ClaudeSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var openAi = await installService.InstallAsync(packages, openAiRequest, CancellationToken.None);
        var claude = await installService.InstallAsync(packages, claudeRequest, CancellationToken.None);
        Assert.True(openAi.IsSuccess, openAi.Failure?.Message);
        Assert.True(claude.IsSuccess, claude.Failure?.Message);
        var updatedPackages = SkillTestData.ReplacePackage(packages, SkillTestData.CreatePackageWithUpdatedBody(packages[0]));

        var result = await updateService.UpdateAsync(new SkillUpdateInput(updatedPackages, openAiRequest), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var updatedManifest = updatedPackages[0].Files.Single(static file => file.RelativePath == "ucli-skill.json").Content;
        var oldManifest = packages[0].Files.Single(static file => file.RelativePath == "ucli-skill.json").Content;
        Assert.Equal(updatedManifest, File.ReadAllText(Path.Combine(openAi.Value!.TargetRoot, packages[0].Manifest.SkillName, "ucli-skill.json")));
        Assert.Equal(oldManifest, File.ReadAllText(Path.Combine(claude.Value!.TargetRoot, packages[0].Manifest.SkillName, "ucli-skill.json")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_RejectsUnsafePackageName ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "update-unsafe-package");
        var generatedPackage = (await SkillTestData.GenerateOfficialPackagesAsync()).First();
        var package = generatedPackage with
        {
            Manifest = generatedPackage.Manifest with
            {
                SkillName = "../escape",
            },
        };
        var service = SkillTestData.CreateUpdateService();

        var result = await service.UpdateAsync(
            new SkillUpdateInput(
                [package],
                new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath)),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.PathUnsafe, result.Failure!.Code);
    }

    private sealed class FailingPackageWriter : ISkillMaterializedPackageWriter
    {
        public ValueTask<SkillOperationResult<bool>> WriteAsync (
            string targetRoot,
            string skillDirectory,
            SkillMaterializedPackage materializedPackage,
            SkillMaterializedPackageWriteMode writeMode,
            Func<string, CancellationToken, ValueTask<SkillOperationResult<bool>>>? precondition,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(SkillOperationResult<bool>.FailureResult(
                SkillFailureCodes.InstallTargetWriteFailed,
                "Synthetic write failure."));
        }
    }
}
