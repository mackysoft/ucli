using MackySoft.Ucli.Application.Features.Recording.Artifacts;
using MackySoft.Ucli.Application.Features.Recording.Capability;
using MackySoft.Ucli.Application.Features.Recording.Finalization;
using MackySoft.Ucli.Application.Features.Recording.Registry;
using MackySoft.Ucli.Features.Recording.Artifacts;
using MackySoft.Ucli.Features.Recording.Artifacts.Mp4;
using MackySoft.Ucli.Features.Recording.Capability;
using MackySoft.Ucli.Features.Recording.Finalization;
using MackySoft.Ucli.Features.Recording.Registry;
using MackySoft.Ucli.Infrastructure.Artifacts;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.Ucli.Hosting.Composition.Features;

/// <summary>Registers host adapters for GameView recording workflows.</summary>
internal static class RecordingServiceCollectionExtensions
{
    /// <summary>Registers Recorder package observation, durable state, and terminal artifact publication.</summary>
    public static IServiceCollection AddUcliRecordingFeatureServices (
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<GameViewRecordingMp4Validator>();
        services.AddSingleton<IGameViewRecorderPackageResolver, FileGameViewRecorderPackageResolver>();
        services.AddSingleton<IGameViewRecordingArtifactStore, FileGameViewRecordingArtifactStore>();
        services.AddSingleton<IGameViewRecordingExecutionStore, FileGameViewRecordingExecutionStore>();
        services.AddSingleton<IGameViewRecordingTerminalFinalizer, GameViewRecordingTerminalFinalizer>();
        return services;
    }
}
