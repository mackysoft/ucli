using MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Artifacts;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Input;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Profiles;
using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Features.Assurance.Build;
using MackySoft.Ucli.Features.Assurance.Compile;
using MackySoft.Ucli.Features.Assurance.Verify;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.Ucli.Hosting.Composition.Features;

/// <summary> Registers feature services for assurance workflows. </summary>
internal static class AssuranceServiceCollectionExtensions
{
    /// <summary> Registers assurance feature services. </summary>
    public static IServiceCollection AddUcliAssuranceFeatureServices (this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<BuildOutputManifestJsonContractWriter>();
        services.AddSingleton<BuildRunMetadataDocumentWriter>();
        services.AddSingleton<IBuildRunArtifactStore, FileBuildRunArtifactStore>();
        services.AddSingleton<FileCompileRunArtifactReader>();
        services.AddSingleton<ICompileRunArtifactReader>(static serviceProvider => serviceProvider.GetRequiredService<FileCompileRunArtifactReader>());
        services.AddSingleton<ICompileRunArtifactStore>(static serviceProvider => serviceProvider.GetRequiredService<FileCompileRunArtifactReader>());
        services.AddSingleton<IVerifyProfileFileReader, FileVerifyProfileFileReader>();
        services.AddSingleton<IVerifyFromInputFileReader, FileVerifyFromInputFileReader>();
        return services;
    }
}
