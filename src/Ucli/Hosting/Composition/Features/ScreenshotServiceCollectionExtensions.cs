using MackySoft.Ucli.Application.Features.Screenshot.Artifacts;
using MackySoft.Ucli.Features.Screenshot.Artifacts;
using MackySoft.Ucli.Features.Screenshot.Artifacts.Png;
using MackySoft.Ucli.Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.Ucli.Hosting.Composition.Features;

/// <summary> Registers host adapters for screenshot capture workflows. </summary>
internal static class ScreenshotServiceCollectionExtensions
{
    /// <summary> Registers screenshot artifact encoding and storage services. </summary>
    public static IServiceCollection AddUcliScreenshotFeatureServices (this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<Rgba8SrgbPngEncoder>();
        services.AddSingleton<Rgba8SrgbPngValidator>();
        services.AddSingleton<IScreenshotArtifactStore>(serviceProvider =>
            new FileScreenshotArtifactStore(
                serviceProvider.GetRequiredService<Rgba8SrgbPngEncoder>(),
                serviceProvider.GetRequiredService<Rgba8SrgbPngValidator>(),
                TimeProvider.System,
                FileSystemAccessBoundary.EnsureSecureDirectory,
                File.Delete));
        return services;
    }
}
