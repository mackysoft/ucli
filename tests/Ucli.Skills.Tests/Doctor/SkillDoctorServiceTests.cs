using MackySoft.Tests;
using MackySoft.Ucli.Skills.Doctor;
using MackySoft.Ucli.Skills.Hosts.Claude;
using MackySoft.Ucli.Skills.Hosts.OpenAi;
using MackySoft.Ucli.Skills.Installation;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Tests.Doctor;

public sealed class SkillDoctorServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task DiagnoseAsync_ReturnsHealthy_WhenTargetMatchesHost ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "doctor-healthy");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var installResult = await installService.InstallAsync(
            packages,
            new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath),
            CancellationToken.None);
        Assert.True(installResult.IsSuccess, installResult.Failure?.Message);
        var doctor = SkillTestData.CreateDoctorService();

        var result = await doctor.DiagnoseAsync(packages, OpenAiSkillHostAdapter.HostKey, installResult.Value!.TargetRoot, CancellationToken.None);

        Assert.True(result.IsHealthy);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == "SKILL_DOCTOR_OK");
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DiagnoseAsync_ReportsMissingSkillDirectory ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "doctor-missing");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var doctor = SkillTestData.CreateDoctorService();
        var targetRoot = scope.CreateDirectory(".agents/skills");

        var result = await doctor.DiagnoseAsync(packages, OpenAiSkillHostAdapter.HostKey, targetRoot, CancellationToken.None);

        Assert.False(result.IsHealthy);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == SkillFailureCodes.InstallTargetUnmanaged);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DiagnoseAsync_ReportsHostConflict_WhenTargetWasMaterializedForDifferentHost ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "doctor-host-conflict");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var installResult = await installService.InstallAsync(
            packages,
            new SkillInstallRequest(ClaudeSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath, "shared-skills"),
            CancellationToken.None);
        Assert.True(installResult.IsSuccess, installResult.Failure?.Message);
        var doctor = SkillTestData.CreateDoctorService();

        var result = await doctor.DiagnoseAsync(packages, OpenAiSkillHostAdapter.HostKey, installResult.Value!.TargetRoot, CancellationToken.None);

        Assert.False(result.IsHealthy);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == SkillFailureCodes.InstallTargetHostConflict);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DiagnoseAsync_ReportsDigestMismatch_WhenInstalledSkillBodyChanged ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "doctor-body-drift");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var installResult = await installService.InstallAsync(
            packages,
            new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath),
            CancellationToken.None);
        Assert.True(installResult.IsSuccess, installResult.Failure?.Message);
        File.AppendAllText(Path.Combine(installResult.Value!.TargetRoot, packages[0].SkillName, "SKILL.md"), "\nInjected instruction.\n");
        var doctor = SkillTestData.CreateDoctorService();

        var result = await doctor.DiagnoseAsync(packages, OpenAiSkillHostAdapter.HostKey, installResult.Value.TargetRoot, CancellationToken.None);

        Assert.False(result.IsHealthy);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == SkillFailureCodes.InstallTargetDigestMismatch);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DiagnoseAsync_ReportsDigestMismatch_WhenInstalledReferenceIsMissing ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "doctor-reference-missing");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var installResult = await installService.InstallAsync(
            packages,
            new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath),
            CancellationToken.None);
        Assert.True(installResult.IsSuccess, installResult.Failure?.Message);
        var referencePath = Path.Combine(
            installResult.Value!.TargetRoot,
            packages[0].SkillName,
            packages[0].Files.First(static file => file.RelativePath.StartsWith("references/", StringComparison.Ordinal)).RelativePath);
        File.Delete(referencePath);
        var doctor = SkillTestData.CreateDoctorService();

        var result = await doctor.DiagnoseAsync(packages, OpenAiSkillHostAdapter.HostKey, installResult.Value.TargetRoot, CancellationToken.None);

        Assert.False(result.IsHealthy);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == SkillFailureCodes.InstallTargetDigestMismatch);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DiagnoseAsync_ReportsInvalidManifestWithoutThrowing ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "doctor-invalid-manifest");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var targetRoot = scope.CreateDirectory(".agents/skills");
        scope.WriteFile(Path.Combine(".agents", "skills", packages[0].SkillName, "ucli-skill.json"), "{}");
        var doctor = SkillTestData.CreateDoctorService();

        var result = await doctor.DiagnoseAsync(packages, OpenAiSkillHostAdapter.HostKey, targetRoot, CancellationToken.None);

        Assert.False(result.IsHealthy);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == SkillFailureCodes.ManifestInvalid);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DiagnoseAsync_ReportsPathUnsafe_WhenManifestSymlinkEscapesTarget ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("ucli-skills", "doctor-manifest-symlink");
        using var outsideScope = TestDirectories.CreateTempScope("ucli-skills", "doctor-manifest-symlink-outside");
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

        var doctor = SkillTestData.CreateDoctorService();

        var result = await doctor.DiagnoseAsync(packages, OpenAiSkillHostAdapter.HostKey, targetRoot, CancellationToken.None);

        Assert.False(result.IsHealthy);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == SkillFailureCodes.PathUnsafe);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DiagnoseAsync_ReportsUnsupportedHost_WhenHostIsUnknown ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "doctor-unsupported-host");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var doctor = SkillTestData.CreateDoctorService();

        var result = await doctor.DiagnoseAsync(packages, "generic", scope.FullPath, CancellationToken.None);

        Assert.False(result.IsHealthy);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == SkillFailureCodes.HostUnsupported);
    }
}
