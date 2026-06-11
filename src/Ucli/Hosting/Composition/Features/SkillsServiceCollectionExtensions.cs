using MackySoft.AgentSkills.Digests;
using MackySoft.AgentSkills.Distribution;
using MackySoft.AgentSkills.Doctor;
using MackySoft.AgentSkills.Hosts.Claude;
using MackySoft.AgentSkills.Hosts.Copilot;
using MackySoft.AgentSkills.Hosts.OpenAi;
using MackySoft.AgentSkills.Hosts.Registration;
using MackySoft.AgentSkills.Installation.Contracts;
using MackySoft.AgentSkills.Installation.Diffing;
using MackySoft.AgentSkills.Installation.Inventory;
using MackySoft.AgentSkills.Installation.Services;
using MackySoft.AgentSkills.Installation.State;
using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.Installation.Transactions;
using MackySoft.AgentSkills.Installation.Validation;
using MackySoft.AgentSkills.Manifests;
using MackySoft.AgentSkills.Materialization;
using MackySoft.AgentSkills.Packaging.Canonical;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.Ucli.Hosting.Composition.Features;

/// <summary> Provides DI registration for uCLI SKILL distribution commands. </summary>
internal static class SkillsServiceCollectionExtensions
{
    /// <summary> Registers services required by <c>ucli skills</c> commands. </summary>
    /// <param name="services"> The target service collection. </param>
    /// <returns> The updated service collection. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="services" /> is <see langword="null" />. </exception>
    public static IServiceCollection AddUcliSkillsFeatureServices (this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(_ => new SkillHostAdapterSet(
        [
            new ClaudeSkillHostAdapter(),
            new CopilotSkillHostAdapter(),
            new OpenAiSkillHostAdapter(),
        ]));
        services.AddSingleton(_ => new BundledSkillPackageRootResolver(AppContext.BaseDirectory));
        services.AddSingleton<SkillDigestCalculator>();
        services.AddSingleton<SkillManifestJsonSerializer>();
        services.AddSingleton<SkillManifestDigestCalculator>();
        services.AddSingleton<SkillManifestValidator>();
        services.AddSingleton<CanonicalSkillPackageReader>();
        services.AddSingleton<SkillPackageProvider>();
        services.AddSingleton<SkillMaterializationService>();
        services.AddSingleton<SkillExportService>();
        services.AddSingleton(_ => new SkillUserTargetRootResolver(
            () => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetEnvironmentVariable));
        services.AddSingleton<SkillInstallTargetResolver>();
        services.AddSingleton<SkillInstalledManifestReader>();
        services.AddSingleton<SkillInstalledContentDigestVerifier>();
        services.AddSingleton<SkillInstalledFileSetVerifier>();
        services.AddSingleton<SkillHostMaterializationInspector>();
        services.AddSingleton<SkillInstalledPackageValidator>();
        services.AddSingleton<SkillInstalledPackageIntegrityVerifier>();
        services.AddSingleton<SkillInstalledTargetStateAnalyzer>();
        services.AddSingleton<ISkillPackageDirectoryOperations, SkillPackageDirectoryOperations>();
        services.AddSingleton<ISkillMaterializedPackageWriter, SkillMaterializedPackageWriter>();
        services.AddSingleton<ISkillInstalledPackageRemover, SkillInstalledPackageRemover>();
        services.AddSingleton<SkillMaterializedPackageDiffBuilder>();
        services.AddSingleton<SkillInstallService>();
        services.AddSingleton<SkillUpdateService>();
        services.AddSingleton<SkillUninstallService>();
        services.AddSingleton<SkillInstallationScanner>();
        services.AddSingleton<SkillDoctorService>();

        return services;
    }
}
