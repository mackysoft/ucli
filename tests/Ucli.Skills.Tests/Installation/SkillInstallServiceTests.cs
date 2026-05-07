using MackySoft.Tests;
using MackySoft.Ucli.Skills.Hosts.Claude;
using MackySoft.Ucli.Skills.Hosts.OpenAi;
using MackySoft.Ucli.Skills.Installation;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Tests.Installation;

public sealed class SkillInstallServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_CreatesThenNoOps_WhenTargetMatchesSameHost ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "install-noop");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var service = SkillTestData.CreateInstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);

        var created = await service.InstallAsync(packages, request, CancellationToken.None);
        var noOp = await service.InstallAsync(packages, request, CancellationToken.None);

        Assert.True(created.IsSuccess, created.Failure?.Message);
        Assert.True(noOp.IsSuccess, noOp.Failure?.Message);
        Assert.All(created.Value!.Actions, static action => Assert.Equal(SkillInstallActionKind.Created, action.ActionKind));
        Assert.All(noOp.Value!.Actions, static action => Assert.Equal(SkillInstallActionKind.NoOp, action.ActionKind));
        foreach (var package in packages)
        {
            var expectedManifest = package.Files.Single(static file => file.RelativePath == "ucli-skill.json").Content;
            var actualManifest = File.ReadAllText(Path.Combine(scope.FullPath, ".agents", "skills", package.Manifest.SkillName, "ucli-skill.json"));
            Assert.Equal(expectedManifest, actualManifest);
        }

        Assert.True(File.Exists(Path.Combine(scope.FullPath, ".agents", "skills", packages[0].Manifest.SkillName, "agents", "openai.yaml")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_DryRunCreatesPlanWithoutWritingFiles ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "install-dry-run-create");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var service = SkillTestData.CreateInstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);

        var result = await service.InstallAsync(new SkillInstallInput(packages, request, DryRun: true, PrintDiff: true), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.True(result.Value!.DryRun);
        Assert.All(result.Value.Actions, static action => Assert.Equal(SkillInstallActionKind.Created, action.ActionKind));
        Assert.All(result.Value.Actions, static action => Assert.NotEmpty(action.Diffs!));
        Assert.False(Directory.Exists(result.Value.TargetRoot));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_DryRunBlocksManagedOverwriteWithoutWriting ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "install-dry-run-managed-overwrite");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var service = SkillTestData.CreateInstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await service.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var skillPath = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName, "SKILL.md");
        var originalSkill = File.ReadAllText(skillPath);
        var updatedPackages = SkillTestData.ReplacePackage(packages, SkillTestData.CreatePackageWithUpdatedBody(packages[0]));

        var result = await service.InstallAsync(
            new SkillInstallInput(updatedPackages, request, DryRun: true, PrintDiff: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = result.Value!.Actions.Single(action => action.Identity.SkillName == packages[0].Manifest.SkillName);
        Assert.Equal(SkillInstallActionKind.BlockedManagedOverwrite, action.ActionKind);
        Assert.Equal(SkillBlockedReason.ManagedOverwriteRequiresForce, action.BlockedReason);
        Assert.NotEmpty(action.Diffs!);
        Assert.Equal(originalSkill, File.ReadAllText(skillPath));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_RejectsSharedTargetRootFromDifferentHost ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "install-host-conflict");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var service = SkillTestData.CreateInstallService();
        var targetRoot = "shared-skills";

        var claude = await service.InstallAsync(
            packages,
            new SkillInstallRequest(ClaudeSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath, targetRoot),
            CancellationToken.None);
        var openAi = await service.InstallAsync(
            packages,
            new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath, targetRoot),
            CancellationToken.None);

        Assert.True(claude.IsSuccess, claude.Failure?.Message);
        Assert.False(openAi.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetHostConflict, openAi.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_RejectsOpenAiSharedTargetRootFromDifferentHost ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "install-openai-host-conflict");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var service = SkillTestData.CreateInstallService();
        var targetRoot = "shared-skills";

        var openAi = await service.InstallAsync(
            packages,
            new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath, targetRoot),
            CancellationToken.None);
        var claude = await service.InstallAsync(
            packages,
            new SkillInstallRequest(ClaudeSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath, targetRoot),
            CancellationToken.None);

        Assert.True(openAi.IsSuccess, openAi.Failure?.Message);
        Assert.False(claude.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetHostConflict, claude.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_RejectsExistingUnmanagedTarget ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "install-unmanaged");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var service = SkillTestData.CreateInstallService();
        scope.WriteFile(Path.Combine(".agents", "skills", packages[0].Manifest.SkillName, "SKILL.md"), "# Existing\n");

        var result = await service.InstallAsync(
            packages,
            new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetUnmanaged, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_DryRunBlocksUnmanagedTargetWithoutDiffContent ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "install-dry-run-unmanaged-no-diff-content");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var service = SkillTestData.CreateInstallService();
        var unmanagedPath = scope.WriteFile(Path.Combine(".agents", "skills", packages[0].Manifest.SkillName, "SKILL.md"), "# Existing\nsecret=local\n");

        var result = await service.InstallAsync(
            new SkillInstallInput(
                [packages[0]],
                new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath),
                DryRun: true,
                PrintDiff: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = result.Value!.Actions.Single();
        Assert.Equal(SkillInstallActionKind.BlockedUnmanaged, action.ActionKind);
        Assert.Equal(SkillBlockedReason.UnmanagedTarget, action.BlockedReason);
        Assert.Empty(action.Diffs!);
        Assert.True(File.Exists(unmanagedPath));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_WhenLaterTargetIsUnmanaged_DoesNotCreateEarlierPackage ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "install-plan-before-write-unmanaged");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var service = SkillTestData.CreateInstallService();
        var firstSkillDirectory = Path.Combine(scope.FullPath, ".agents", "skills", packages[0].Manifest.SkillName);
        var unmanagedPath = scope.WriteFile(Path.Combine(".agents", "skills", packages[1].Manifest.SkillName, "SKILL.md"), "# Existing\n");

        var result = await service.InstallAsync(
            [packages[0], packages[1]],
            new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetUnmanaged, result.Failure!.Code);
        Assert.False(Directory.Exists(firstSkillDirectory));
        Assert.True(File.Exists(unmanagedPath));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_RejectsDifferentContentDigest ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "install-digest-mismatch");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var service = SkillTestData.CreateInstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var created = await service.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(created.IsSuccess, created.Failure?.Message);

        var manifestPath = Path.Combine(created.Value!.TargetRoot, packages[0].Manifest.SkillName, "ucli-skill.json");
        var manifestText = File.ReadAllText(manifestPath).Replace(packages[0].Manifest.ContentDigest, "sha256:" + new string('0', 64), StringComparison.Ordinal);
        File.WriteAllText(manifestPath, manifestText);

        var result = await service.InstallAsync(packages, request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetDigestMismatch, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_RejectsModifiedCanonicalManifest ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "install-manifest-drift");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var service = SkillTestData.CreateInstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var created = await service.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(created.IsSuccess, created.Failure?.Message);

        var manifestPath = Path.Combine(created.Value!.TargetRoot, packages[0].Manifest.SkillName, "ucli-skill.json");
        var originalDigest = packages[0].Manifest.HostArtifacts[0].MaterializedFrontmatterDigest;
        var manifestText = File.ReadAllText(manifestPath).Replace(originalDigest, "sha256:" + new string('f', 64), StringComparison.Ordinal);
        File.WriteAllText(manifestPath, manifestText);

        var result = await service.InstallAsync(packages, request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.ManifestInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_RejectsModifiedInstalledSkillBody ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "install-body-drift");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var service = SkillTestData.CreateInstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var created = await service.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(created.IsSuccess, created.Failure?.Message);

        var skillPath = Path.Combine(created.Value!.TargetRoot, packages[0].Manifest.SkillName, "SKILL.md");
        File.AppendAllText(skillPath, "\nInjected instruction.\n");

        var result = await service.InstallAsync(packages, request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetDigestMismatch, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_RejectsMissingInstalledSkillBodyWithoutThrowing ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "install-body-missing");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var service = SkillTestData.CreateInstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var created = await service.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(created.IsSuccess, created.Failure?.Message);

        File.Delete(Path.Combine(created.Value!.TargetRoot, packages[0].Manifest.SkillName, "SKILL.md"));

        var result = await service.InstallAsync(packages, request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetDigestMismatch, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_RejectsModifiedInstalledReference ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "install-reference-drift");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var service = SkillTestData.CreateInstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var created = await service.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(created.IsSuccess, created.Failure?.Message);

        var referencePath = Path.Combine(
            created.Value!.TargetRoot,
            packages[0].Manifest.SkillName,
            packages[0].Files.First(static file => file.RelativePath.StartsWith("references/", StringComparison.Ordinal)).RelativePath);
        File.AppendAllText(referencePath, "\nInjected reference.\n");

        var result = await service.InstallAsync(packages, request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetDigestMismatch, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_RejectsUnexpectedInstalledFile ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "install-extra-file");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var service = SkillTestData.CreateInstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var created = await service.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(created.IsSuccess, created.Failure?.Message);
        File.WriteAllText(Path.Combine(created.Value!.TargetRoot, packages[0].Manifest.SkillName, "references", "extra.md"), "# Extra\n");

        var result = await service.InstallAsync(packages, request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetDigestMismatch, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_RejectsInvalidExistingManifestWithoutThrowing ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "install-invalid-manifest");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var service = SkillTestData.CreateInstallService();
        scope.WriteFile(Path.Combine(".agents", "skills", packages[0].Manifest.SkillName, "ucli-skill.json"), "{}");

        var result = await service.InstallAsync(
            packages,
            new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.ManifestInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_RejectsManifestSymlinkThatEscapesTarget ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("ucli-skills", "install-manifest-symlink");
        using var outsideScope = TestDirectories.CreateTempScope("ucli-skills", "install-manifest-symlink-outside");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var service = SkillTestData.CreateInstallService();
        scope.CreateDirectory(Path.Combine(".agents", "skills", packages[0].Manifest.SkillName));
        var outsideManifest = outsideScope.WriteFile("ucli-skill.json", packages[0].Files.Single(static file => file.RelativePath == "ucli-skill.json").Content);
        try
        {
            File.CreateSymbolicLink(Path.Combine(scope.FullPath, ".agents", "skills", packages[0].Manifest.SkillName, "ucli-skill.json"), outsideManifest);
        }
        catch (IOException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        var result = await service.InstallAsync(
            packages,
            new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.PathUnsafe, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_ReturnsUnsupportedHostFailure_WhenHostIsUnknown ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "install-unsupported-host");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var service = SkillTestData.CreateInstallService();

        var result = await service.InstallAsync(
            packages,
            new SkillInstallRequest("generic", SkillScopeKind.Project, scope.FullPath),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.HostUnsupported, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_RejectsUnsafePackageName ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "install-unsafe-package");
        var generatedPackage = (await SkillTestData.GenerateOfficialPackagesAsync()).First();
        var package = generatedPackage with
        {
            Manifest = generatedPackage.Manifest with
            {
                SkillName = "../escape",
            },
        };
        var service = SkillTestData.CreateInstallService();

        var result = await service.InstallAsync(
            [package],
            new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.PathUnsafe, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_RejectsTargetRootOutsideRepository ()
    {
        using var repoScope = TestDirectories.CreateTempScope("ucli-skills", "install-path-repo");
        using var outsideScope = TestDirectories.CreateTempScope("ucli-skills", "install-path-outside");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var service = SkillTestData.CreateInstallService();

        var result = await service.InstallAsync(
            packages,
            new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, repoScope.FullPath, outsideScope.FullPath),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.PathUnsafe, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_RejectsTargetRootThatEscapesThroughSymlink ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var repoScope = TestDirectories.CreateTempScope("ucli-skills", "install-path-symlink-repo");
        using var outsideScope = TestDirectories.CreateTempScope("ucli-skills", "install-path-symlink-outside");
        var symlinkPath = Path.Combine(repoScope.FullPath, "linked");
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

        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var service = SkillTestData.CreateInstallService();

        var result = await service.InstallAsync(
            packages,
            new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, repoScope.FullPath, "linked/skills"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.PathUnsafe, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_RejectsExistingSkillDirectoryThatEscapesThroughSymlink ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var repoScope = TestDirectories.CreateTempScope("ucli-skills", "install-skill-symlink-repo");
        using var outsideScope = TestDirectories.CreateTempScope("ucli-skills", "install-skill-symlink-outside");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var targetRoot = repoScope.CreateDirectory(".agents/skills");
        var symlinkPath = Path.Combine(targetRoot, packages[0].Manifest.SkillName);
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

        var service = SkillTestData.CreateInstallService();

        var result = await service.InstallAsync(
            packages,
            new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, repoScope.FullPath),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.PathUnsafe, result.Failure!.Code);
    }
}
