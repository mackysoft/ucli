using MackySoft.Tests;
using MackySoft.Ucli.Skills.Hosts;
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
        var request = new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath);

        var created = await service.InstallAsync(packages, request, CancellationToken.None);
        var noOp = await service.InstallAsync(packages, request, CancellationToken.None);

        Assert.True(created.IsSuccess, created.Failure?.Message);
        Assert.True(noOp.IsSuccess, noOp.Failure?.Message);
        Assert.All(created.Value!.Actions, static action => Assert.Equal(SkillInstallActionKind.Created, action.ActionKind));
        Assert.All(noOp.Value!.Actions, static action => Assert.Equal(SkillInstallActionKind.NoOp, action.ActionKind));
        Assert.True(File.Exists(Path.Combine(scope.FullPath, ".agents", "skills", packages[0].SkillName, "ucli-skill.json")));
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
            new SkillInstallRequest(SkillHostKind.Claude, SkillScopeKind.Project, scope.FullPath, targetRoot),
            CancellationToken.None);
        var openAi = await service.InstallAsync(
            packages,
            new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath, targetRoot),
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
            new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath),
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
        var request = new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath);
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
    public async Task InstallAsync_RejectsTargetRootOutsideRepository ()
    {
        using var repoScope = TestDirectories.CreateTempScope("ucli-skills", "install-path-repo");
        using var outsideScope = TestDirectories.CreateTempScope("ucli-skills", "install-path-outside");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var service = new SkillInstallService();

        var result = await service.InstallAsync(
            packages,
            new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, repoScope.FullPath, outsideScope.FullPath),
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
            new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, repoScope.FullPath, "linked/skills"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.PathUnsafe, result.Failure!.Code);
    }
}
