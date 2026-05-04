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
        var service = new SkillInstallService();
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
            var actualManifest = File.ReadAllText(Path.Combine(scope.FullPath, ".agents", "skills", package.SkillName, "ucli-skill.json"));
            Assert.Equal(expectedManifest, actualManifest);
        }

        Assert.True(File.Exists(Path.Combine(scope.FullPath, ".agents", "skills", packages[0].SkillName, "agents", "openai.yaml")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_RejectsSharedTargetRootFromDifferentHost ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "install-host-conflict");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var service = new SkillInstallService();
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
    public async Task InstallAsync_RejectsExistingUnmanagedTarget ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "install-unmanaged");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var service = new SkillInstallService();
        scope.WriteFile(Path.Combine(".agents", "skills", packages[0].SkillName, "SKILL.md"), "# Existing\n");

        var result = await service.InstallAsync(
            packages,
            new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetUnmanaged, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_RejectsDifferentContentDigest ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "install-digest-mismatch");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var service = new SkillInstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var created = await service.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(created.IsSuccess, created.Failure?.Message);

        var manifestPath = Path.Combine(created.Value!.TargetRoot, packages[0].SkillName, "ucli-skill.json");
        var manifestText = File.ReadAllText(manifestPath).Replace(packages[0].Manifest.ContentDigest, "sha256:" + new string('0', 64), StringComparison.Ordinal);
        File.WriteAllText(manifestPath, manifestText);

        var result = await service.InstallAsync(packages, request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetDigestMismatch, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_RejectsModifiedInstalledSkillBody ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "install-body-drift");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var service = new SkillInstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var created = await service.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(created.IsSuccess, created.Failure?.Message);

        var skillPath = Path.Combine(created.Value!.TargetRoot, packages[0].SkillName, "SKILL.md");
        File.AppendAllText(skillPath, "\nInjected instruction.\n");

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
        var service = new SkillInstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var created = await service.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(created.IsSuccess, created.Failure?.Message);

        var referencePath = Path.Combine(
            created.Value!.TargetRoot,
            packages[0].SkillName,
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
        var service = new SkillInstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var created = await service.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(created.IsSuccess, created.Failure?.Message);
        File.WriteAllText(Path.Combine(created.Value!.TargetRoot, packages[0].SkillName, "references", "extra.md"), "# Extra\n");

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
        var service = new SkillInstallService();
        scope.WriteFile(Path.Combine(".agents", "skills", packages[0].SkillName, "ucli-skill.json"), "{}");

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
        var service = new SkillInstallService();
        scope.CreateDirectory(Path.Combine(".agents", "skills", packages[0].SkillName));
        var outsideManifest = outsideScope.WriteFile("ucli-skill.json", packages[0].Files.Single(static file => file.RelativePath == "ucli-skill.json").Content);
        try
        {
            File.CreateSymbolicLink(Path.Combine(scope.FullPath, ".agents", "skills", packages[0].SkillName, "ucli-skill.json"), outsideManifest);
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
        var service = new SkillInstallService();

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
        var package = (await SkillTestData.GenerateOfficialPackagesAsync()).First() with
        {
            SkillName = "../escape",
        };
        var service = new SkillInstallService();

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
        var service = new SkillInstallService();

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
        var service = new SkillInstallService();

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
        var symlinkPath = Path.Combine(targetRoot, packages[0].SkillName);
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

        var service = new SkillInstallService();

        var result = await service.InstallAsync(
            packages,
            new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, repoScope.FullPath),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.PathUnsafe, result.Failure!.Code);
    }
}
