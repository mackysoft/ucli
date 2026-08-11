using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityRequest;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Recording;

namespace MackySoft.Ucli.Application.Features.Recording.Capability;

/// <summary>Combines the resolved package graph with an optional live Unity adapter observation.</summary>
internal sealed class GameViewRecordingCapabilityResolver
{
    private readonly IGameViewRecorderPackageResolver packageResolver;
    private readonly IUnityRequestExecutor unityRequestExecutor;

    public GameViewRecordingCapabilityResolver (
        IGameViewRecorderPackageResolver packageResolver,
        IUnityRequestExecutor unityRequestExecutor)
    {
        this.packageResolver = packageResolver ?? throw new ArgumentNullException(nameof(packageResolver));
        this.unityRequestExecutor = unityRequestExecutor ?? throw new ArgumentNullException(nameof(unityRequestExecutor));
    }

    public async ValueTask<GameViewRecordingCapabilityResolution> ResolveAsync (
        ProjectContext context,
        UcliCommand command,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(command);

        var package = await packageResolver
            .ResolveAsync(context.UnityProject, cancellationToken)
            .ConfigureAwait(false);
        return package.State switch
        {
            GameViewRecorderPackageResolutionState.Missing => Resolve(CreateUnavailable()),
            GameViewRecorderPackageResolutionState.Indeterminate => Resolve(CreatePackageIndeterminate()),
            GameViewRecorderPackageResolutionState.Resolved => await ResolveVersionAsync(
                    context,
                    command,
                    timeout,
                    package.Version!,
                    cancellationToken)
                .ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(package), package.State, "Recorder package state is not defined."),
        };
    }

    private async ValueTask<GameViewRecordingCapabilityResolution> ResolveVersionAsync (
        ProjectContext context,
        UcliCommand command,
        TimeSpan timeout,
        string version,
        CancellationToken cancellationToken)
    {
        var package = new GameViewRecordingPackageCapability(
            GameViewRecordingPackageState.Resolved,
            GameViewRecorderCompatibilityMetadata.PackageId,
            version);
        if (!GameViewRecorderVersionCompatibility.TryEvaluate(version, out var supported))
        {
            return Resolve(new GameViewRecordingCapability(
                package,
                new GameViewRecordingCompatibilityCapability(
                    GameViewRecordingCompatibilityState.Indeterminate,
                    GameViewRecorderCompatibilityMetadata.RecorderPackageVersionRange,
                    resolvedVersion: null),
                new GameViewRecordingAdapterCapability(
                    GameViewRecordingAdapterState.Unobserved,
                    adapterId: null,
                    adapterVersion: null),
                NotObserved(GameViewRecordingErrorCodes.RecorderUnsupported),
                limits: null,
                captureProfile: null));
        }

        if (!supported)
        {
            return Resolve(new GameViewRecordingCapability(
                package,
                new GameViewRecordingCompatibilityCapability(
                    GameViewRecordingCompatibilityState.Unsupported,
                    GameViewRecorderCompatibilityMetadata.RecorderPackageVersionRange,
                    version),
                new GameViewRecordingAdapterCapability(
                    GameViewRecordingAdapterState.NotApplicable,
                    adapterId: null,
                    adapterVersion: null),
                Blocked(GameViewRecordingErrorCodes.RecorderUnsupported),
                limits: null,
                captureProfile: null));
        }

        var compatibility = new GameViewRecordingCompatibilityCapability(
            GameViewRecordingCompatibilityState.Supported,
            GameViewRecorderCompatibilityMetadata.RecorderPackageVersionRange,
            version);
        var execution = await unityRequestExecutor.ExecuteAsync(
                command,
                UnityExecutionMode.Daemon,
                timeout,
                context.Config,
                context.UnityProject,
                new UnityRequestPayload.RecordingCapability(),
                cancellationToken)
            .ConfigureAwait(false);
        if (!execution.IsSuccess)
        {
            return Resolve(CreateRuntimeUnobserved(
                package,
                compatibility,
                execution.FailureInfo?.Code ?? GameViewRecordingErrorCodes.RequiresGuiSession));
        }

        var response = execution.Response!;
        if (response.Errors.Count != 0)
        {
            return Resolve(CreateRuntimeUnobserved(package, compatibility, response.Errors[0].Code));
        }

        if (!IpcPayloadCodec.TryDeserialize(
                response.Payload,
                out IpcGameViewRecordingCapabilityResponse runtime,
                out _))
        {
            return Resolve(CreateRuntimeUnobserved(package, compatibility, GameViewRecordingErrorCodes.AdapterFaulted));
        }

        return GameViewRecordingCapabilityResolution.Create(
            new GameViewRecordingCapability(
                package,
                compatibility,
                runtime.Adapter,
                runtime.RuntimeAdmission,
                runtime.Limits,
                runtime.CaptureProfile),
            runtime.StartBinding,
            runtime.ObservedRuntime);
    }

    private static GameViewRecordingCapability CreateUnavailable () =>
        new(
            new GameViewRecordingPackageCapability(
                GameViewRecordingPackageState.Missing,
                GameViewRecorderCompatibilityMetadata.PackageId,
                version: null),
            new GameViewRecordingCompatibilityCapability(
                GameViewRecordingCompatibilityState.NotApplicable,
                GameViewRecorderCompatibilityMetadata.RecorderPackageVersionRange,
                resolvedVersion: null),
            new GameViewRecordingAdapterCapability(
                GameViewRecordingAdapterState.NotApplicable,
                adapterId: null,
                adapterVersion: null),
            Blocked(GameViewRecordingErrorCodes.Unavailable),
            limits: null,
            captureProfile: null);

    private static GameViewRecordingCapability CreatePackageIndeterminate () =>
        new(
            new GameViewRecordingPackageCapability(
                GameViewRecordingPackageState.Indeterminate,
                GameViewRecorderCompatibilityMetadata.PackageId,
                version: null),
            new GameViewRecordingCompatibilityCapability(
                GameViewRecordingCompatibilityState.Indeterminate,
                GameViewRecorderCompatibilityMetadata.RecorderPackageVersionRange,
                resolvedVersion: null),
            new GameViewRecordingAdapterCapability(
                GameViewRecordingAdapterState.Unobserved,
                adapterId: null,
                adapterVersion: null),
            NotObserved(GameViewRecordingErrorCodes.Unavailable),
            limits: null,
            captureProfile: null);

    private static GameViewRecordingCapability CreateRuntimeUnobserved (
        GameViewRecordingPackageCapability package,
        GameViewRecordingCompatibilityCapability compatibility,
        UcliCode reason) =>
        new(
            package,
            compatibility,
            new GameViewRecordingAdapterCapability(
                GameViewRecordingAdapterState.Unobserved,
                adapterId: null,
                adapterVersion: null),
            NotObserved(reason),
            limits: null,
            captureProfile: null);

    private static GameViewRecordingRuntimeAdmission Blocked (UcliCode reason) =>
        new(GameViewRecordingRuntimeAdmissionState.Blocked, [reason]);

    private static GameViewRecordingRuntimeAdmission NotObserved (UcliCode reason) =>
        new(GameViewRecordingRuntimeAdmissionState.Unobserved, [reason]);

    private static GameViewRecordingCapabilityResolution Resolve (
        GameViewRecordingCapability capability) =>
        GameViewRecordingCapabilityResolution.Create(
            capability,
            startBinding: null,
            observedRuntime: null);
}
