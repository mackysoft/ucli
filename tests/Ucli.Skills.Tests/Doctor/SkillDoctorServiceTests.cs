using MackySoft.Tests;
using MackySoft.Ucli.Skills.Doctor;
using MackySoft.Ucli.Skills.Hosts;
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
        var installService = new SkillInstallService();
        var installResult = await installService.InstallAsync(
            packages,
            new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath),
            CancellationToken.None);
        Assert.True(installResult.IsSuccess, installResult.Failure?.Message);
        var doctor = new SkillDoctorService();

        var result = await doctor.DiagnoseAsync(packages, SkillHostKind.OpenAi, installResult.Value!.TargetRoot, CancellationToken.None);

        Assert.True(result.IsHealthy);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == "SKILL_DOCTOR_OK");
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DiagnoseAsync_ReportsMissingSkillDirectory ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "doctor-missing");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var doctor = new SkillDoctorService();
        var targetRoot = scope.CreateDirectory(".agents/skills");

        var result = await doctor.DiagnoseAsync(packages, SkillHostKind.OpenAi, targetRoot, CancellationToken.None);

        Assert.False(result.IsHealthy);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == SkillFailureCodes.InstallTargetUnmanaged);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DiagnoseAsync_ReportsHostConflict_WhenTargetWasMaterializedForDifferentHost ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "doctor-host-conflict");
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var installService = new SkillInstallService();
        var installResult = await installService.InstallAsync(
            packages,
            new SkillInstallRequest(SkillHostKind.Claude, SkillScopeKind.Project, scope.FullPath, "shared-skills"),
            CancellationToken.None);
        Assert.True(installResult.IsSuccess, installResult.Failure?.Message);
        var doctor = new SkillDoctorService();

        var result = await doctor.DiagnoseAsync(packages, SkillHostKind.OpenAi, installResult.Value!.TargetRoot, CancellationToken.None);

        Assert.False(result.IsHealthy);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == SkillFailureCodes.InstallTargetHostConflict);
    }
}
