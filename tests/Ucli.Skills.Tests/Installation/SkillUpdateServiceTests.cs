using MackySoft.Tests;
using MackySoft.Ucli.Skills.Digests;
using MackySoft.Ucli.Skills.Hosts.Claude;
using MackySoft.Ucli.Skills.Hosts.OpenAi;
using MackySoft.Ucli.Skills.Installation;
using MackySoft.Ucli.Skills.Manifests;
using MackySoft.Ucli.Skills.Packaging;
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
        var updatedPackages = ReplacePackage(packages, CreatePackageWithUpdatedBody(packages[0]));

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
        var updatedPackages = ReplacePackage(packages, CreatePackageWithUpdatedBody(packages[0]));

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

        var updatedPackages = ReplacePackage(packages, CreatePackageWithUpdatedBody(packages[0]));

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
        var updatedPackages = ReplacePackage(packages, CreatePackageWithUpdatedBody(packages[0]));

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

    private static IReadOnlyList<CanonicalSkillPackage> ReplacePackage (
        IReadOnlyList<CanonicalSkillPackage> packages,
        CanonicalSkillPackage replacement)
    {
        return packages
            .Select(package => string.Equals(package.Manifest.SkillName, replacement.Manifest.SkillName, StringComparison.Ordinal) ? replacement : package)
            .ToArray();
    }

    private static CanonicalSkillPackage CreatePackageWithUpdatedBody (CanonicalSkillPackage package)
    {
        var files = package.Files
            .Select(static file => string.Equals(file.RelativePath, "SKILL.md", StringComparison.Ordinal)
                ? SkillPackageFile.Create("SKILL.md", file.Content + "\nOfficial update.\n")
                : file)
            .ToArray();
        var contentDigest = new SkillDigestCalculator().ComputeDigest(files
            .Where(static file => string.Equals(file.RelativePath, "SKILL.md", StringComparison.Ordinal)
                || file.RelativePath.StartsWith("references/", StringComparison.Ordinal))
            .Select(static file => new SkillDigestInputFile(file.RelativePath, file.Content)));
        var manifest = package.Manifest with
        {
            ContentDigest = contentDigest,
        };
        var manifestText = new SkillManifestJsonSerializer().Serialize(manifest);
        files = files
            .Select(file => string.Equals(file.RelativePath, "ucli-skill.json", StringComparison.Ordinal)
                ? SkillPackageFile.Create("ucli-skill.json", manifestText)
                : file)
            .ToArray();

        return package with
        {
            Manifest = manifest,
            Files = files,
        };
    }
}
