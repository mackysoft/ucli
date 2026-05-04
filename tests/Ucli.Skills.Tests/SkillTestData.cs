using MackySoft.Ucli.Skills.Distribution;
using MackySoft.Ucli.Skills.Doctor;
using MackySoft.Ucli.Skills.Generation;
using MackySoft.Ucli.Skills.Hosts.Claude;
using MackySoft.Ucli.Skills.Hosts.Copilot;
using MackySoft.Ucli.Skills.Hosts.OpenAi;
using MackySoft.Ucli.Skills.Hosts.Registration;
using MackySoft.Ucli.Skills.Installation;
using MackySoft.Ucli.Skills.Manifests;
using MackySoft.Ucli.Skills.Materialization;

namespace MackySoft.Ucli.Skills.Tests;

internal static class SkillTestData
{
    internal static readonly string[] ExpectedSkillNames =
    [
        "ucli-plan-apply",
        "ucli-read-project",
        "ucli-troubleshoot",
        "ucli-verify-changes",
    ];

    internal static string GetDefinitionsRoot ()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "Ucli.Skills", "SkillDefinitions");

            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate src/Ucli.Skills/SkillDefinitions from the test output directory.");
    }

    internal static async Task<IReadOnlyList<CanonicalSkillPackage>> GenerateOfficialPackagesAsync ()
    {
        var service = CreatePackageGenerationService();
        var result = await service.GenerateAllAsync(GetDefinitionsRoot(), CancellationToken.None);
        Assert.True(result.IsSuccess, result.Failure?.Message);
        return result.Value!;
    }

    internal static SkillHostAdapterSet CreateOfficialHostAdapterSet ()
    {
        return new SkillHostAdapterSet(
        [
            new ClaudeSkillHostAdapter(),
            new CopilotSkillHostAdapter(),
            new OpenAiSkillHostAdapter(),
        ]);
    }

    internal static SkillPackageGenerationService CreatePackageGenerationService ()
    {
        return new SkillPackageGenerationService(CreateOfficialHostAdapterSet());
    }

    internal static SkillManifestValidator CreateManifestValidator ()
    {
        return new SkillManifestValidator(CreateOfficialHostAdapterSet());
    }

    internal static SkillMaterializationService CreateMaterializationService ()
    {
        return new SkillMaterializationService(CreateOfficialHostAdapterSet());
    }

    internal static SkillExportService CreateExportService ()
    {
        return new SkillExportService(CreateMaterializationService());
    }

    internal static SkillInstallService CreateInstallService ()
    {
        return new SkillInstallService(CreateOfficialHostAdapterSet());
    }

    internal static SkillInstallationScanner CreateInstallationScanner ()
    {
        return new SkillInstallationScanner(CreateOfficialHostAdapterSet());
    }

    internal static SkillDoctorService CreateDoctorService ()
    {
        return new SkillDoctorService(CreateOfficialHostAdapterSet());
    }
}
