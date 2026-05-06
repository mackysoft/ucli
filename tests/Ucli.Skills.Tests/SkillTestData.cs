using MackySoft.Ucli.Skills.Digests;
using MackySoft.Ucli.Skills.Distribution;
using MackySoft.Ucli.Skills.Doctor;
using MackySoft.Ucli.Skills.Generation;
using MackySoft.Ucli.Skills.Hosts.Official;
using MackySoft.Ucli.Skills.Hosts.Registration;
using MackySoft.Ucli.Skills.Installation;
using MackySoft.Ucli.Skills.Installation.Validation;
using MackySoft.Ucli.Skills.Manifests;
using MackySoft.Ucli.Skills.Materialization;
using MackySoft.Ucli.Skills.Packaging;
using MackySoft.Ucli.Skills.Sources;

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
        return Path.Combine(GetRepositoryRoot(), "src", "Ucli.Skills", "SkillDefinitions");
    }

    internal static string GetGeneratedSkillsRoot ()
    {
        return Path.Combine(GetRepositoryRoot(), "skills");
    }

    internal static string GetRepositoryRoot ()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "Ucli.Skills", "SkillDefinitions");

            if (Directory.Exists(candidate))
            {
                return directory.FullName;
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
        return OfficialSkillHostAdapters.CreateSet();
    }

    internal static SkillPackageGenerationService CreatePackageGenerationService ()
    {
        return new SkillPackageGenerationService(
            new SkillSourceDefinitionReader(),
            CreateOfficialHostAdapterSet(),
            new SkillDigestCalculator(),
            new SkillManifestJsonSerializer());
    }

    internal static CanonicalSkillPackageReader CreatePackageReader ()
    {
        var hostAdapters = CreateOfficialHostAdapterSet();
        var manifestSerializer = new SkillManifestJsonSerializer();
        return new CanonicalSkillPackageReader(
            hostAdapters,
            new SkillDigestCalculator(),
            manifestSerializer,
            new SkillManifestValidator(hostAdapters));
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
        var hostAdapters = CreateOfficialHostAdapterSet();
        return new SkillInstallService(
            new SkillInstallTargetResolver(hostAdapters),
            new SkillMaterializationService(hostAdapters),
            CreateInstalledPackageValidator(hostAdapters));
    }

    internal static SkillInstallationScanner CreateInstallationScanner ()
    {
        var hostAdapters = CreateOfficialHostAdapterSet();
        return new SkillInstallationScanner(
            hostAdapters,
            CreateInstalledManifestReader(hostAdapters),
            CreateInstalledPackageValidator(hostAdapters));
    }

    internal static SkillDoctorService CreateDoctorService ()
    {
        var hostAdapters = CreateOfficialHostAdapterSet();
        return new SkillDoctorService(hostAdapters, CreateInstalledPackageValidator(hostAdapters));
    }

    internal static SkillInstalledPackageValidator CreateInstalledPackageValidator (SkillHostAdapterSet hostAdapters)
    {
        return new SkillInstalledPackageValidator(
            CreateInstalledManifestReader(hostAdapters),
            new SkillMaterializationService(hostAdapters),
            new SkillInstalledContentDigestVerifier(new SkillDigestCalculator()),
            new SkillInstalledFileSetVerifier(),
            new SkillHostMaterializationInspector(hostAdapters, new SkillDigestCalculator()));
    }

    internal static SkillInstalledManifestReader CreateInstalledManifestReader (SkillHostAdapterSet hostAdapters)
    {
        return new SkillInstalledManifestReader(
            new SkillManifestJsonSerializer(),
            new SkillManifestValidator(hostAdapters));
    }
}
