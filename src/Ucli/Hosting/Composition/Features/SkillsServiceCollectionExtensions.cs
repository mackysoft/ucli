using MackySoft.Ucli.Skills.Digests;
using MackySoft.Ucli.Skills.Distribution;
using MackySoft.Ucli.Skills.Doctor;
using MackySoft.Ucli.Skills.Hosts.Official;
using MackySoft.Ucli.Skills.Installation;
using MackySoft.Ucli.Skills.Installation.Validation;
using MackySoft.Ucli.Skills.Manifests;
using MackySoft.Ucli.Skills.Materialization;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.Ucli.Hosting.Composition.Features;

/// <summary> Provides DI registration for official SKILL distribution commands. </summary>
internal static class SkillsServiceCollectionExtensions
{
    /// <summary> Registers services required by <c>ucli skills</c> commands. </summary>
    /// <param name="services"> The target service collection. </param>
    /// <returns> The updated service collection. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="services" /> is <see langword="null" />. </exception>
    public static IServiceCollection AddUcliSkillsFeatureServices (this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(_ => OfficialSkillHostAdapters.CreateSet());
        services.AddSingleton<BundledSkillPackageRootResolver>();
        services.AddSingleton<SkillDigestCalculator>();
        services.AddSingleton<SkillManifestJsonSerializer>();
        services.AddSingleton<SkillManifestValidator>();
        services.AddSingleton<CanonicalSkillPackageReader>();
        services.AddSingleton<OfficialSkillPackageProvider>();
        services.AddSingleton<SkillMaterializationService>();
        services.AddSingleton<SkillExportService>();
        services.AddSingleton<SkillInstallTargetResolver>();
        services.AddSingleton<SkillInstalledManifestReader>();
        services.AddSingleton<SkillInstalledContentDigestVerifier>();
        services.AddSingleton<SkillInstalledFileSetVerifier>();
        services.AddSingleton<SkillHostMaterializationInspector>();
        services.AddSingleton<SkillInstalledPackageValidator>();
        services.AddSingleton<SkillInstallService>();
        services.AddSingleton<SkillInstallationScanner>();
        services.AddSingleton<SkillDoctorService>();

        return services;
    }
}
