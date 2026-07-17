using MackySoft.AgentSkills.Hosting.Composition;
using MackySoft.AgentSkills.Hosting.Reporting;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Skills;
using MackySoft.Ucli.Infrastructure.Storage;
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

        services.AddAgentSkillsCommandRuntime(options =>
        {
            options.ProductName = "uCLI";
            options.PackageBaseDirectory = AppContext.BaseDirectory;
            options.CommandRoot = UcliCommandNames.Skills;
            options.RepositoryRootResolver = UcliStoragePathResolver.ResolveStorageRoot;
        });
        services.AddSingleton<IAgentSkillsCommandResultEmitter, UcliAgentSkillsCommandResultEmitter>();

        return services;
    }
}
