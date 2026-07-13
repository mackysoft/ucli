using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Infrastructure.Paths;
using MackySoft.Ucli.Infrastructure.Project;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.UnityIntegration.Project.Plugin.Cache;
using MackySoft.Ucli.UnityIntegration.Project.Plugin.Marker;

namespace MackySoft.Ucli.UnityIntegration.Project.Plugin;

/// <summary> Locates and validates the uCLI Unity plugin marker within one Unity project. </summary>
internal sealed class UnityUcliPluginLocator : IUnityUcliPluginLocator
{
    private readonly UnityUcliPluginMarkerDiscovery pluginMarkerDiscovery;

    private readonly UnityUcliPluginMarkerValidator pluginMarkerValidator;

    private readonly UnityUcliPluginMarkerCacheCoordinator pluginMarkerCacheCoordinator;

    /// <summary> Initializes a new instance of the <see cref="UnityUcliPluginLocator" /> class. </summary>
    public UnityUcliPluginLocator (
        UnityUcliPluginMarkerDiscovery pluginMarkerDiscovery,
        UnityUcliPluginMarkerValidator pluginMarkerValidator,
        UnityUcliPluginMarkerCacheCoordinator pluginMarkerCacheCoordinator)
    {
        this.pluginMarkerDiscovery = pluginMarkerDiscovery ?? throw new ArgumentNullException(nameof(pluginMarkerDiscovery));
        this.pluginMarkerValidator = pluginMarkerValidator ?? throw new ArgumentNullException(nameof(pluginMarkerValidator));
        this.pluginMarkerCacheCoordinator = pluginMarkerCacheCoordinator ?? throw new ArgumentNullException(nameof(pluginMarkerCacheCoordinator));
    }

    /// <inheritdoc />
    public async ValueTask<UnityUcliPluginLocateResult> LocateAsync (
        string unityProjectRoot,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(unityProjectRoot);

        LocateContext locateContext;
        try
        {
            locateContext = CreateLocateContext(unityProjectRoot);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return UnityUcliPluginLocateResult.InvalidMarker(
                unityProjectRoot,
                ExecutionError.InvalidArgument(
                    $"uCLI Unity plugin marker is invalid. Path='{unityProjectRoot}'. Reason=Unity project path is invalid. {exception.Message}"));
        }

        var cachedLocateResult = await pluginMarkerCacheCoordinator.TryLocateFromCacheAsync(
                locateContext.UnityProjectRoot,
                locateContext.StorageRoot,
                locateContext.ProjectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        if (cachedLocateResult != null)
        {
            return cachedLocateResult;
        }

        var markerPathResult = pluginMarkerDiscovery.TryEnumerateMarkerPaths(locateContext.UnityProjectRoot);
        if (!markerPathResult.IsSuccess)
        {
            return UnityUcliPluginLocateResult.InvalidMarker(markerPathResult.Path!, markerPathResult.Error!);
        }

        if (markerPathResult.MarkerPaths!.Count == 0)
        {
            return UnityUcliPluginLocateResult.NotFound(ExecutionError.InvalidArgument(
                $"Unity project does not contain the uCLI Unity plugin. Expected '{UnityUcliPluginMarkerContract.MarkerFileName}' with pluginId '{UnityUcliPluginMarkerContract.ExpectedPluginId}' under Assets/ or Packages/."));
        }

        foreach (var markerPath in markerPathResult.MarkerPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var markerError = await pluginMarkerValidator.ValidateMarkerAsync(markerPath, cancellationToken).ConfigureAwait(false);
            if (markerError != null)
            {
                return UnityUcliPluginLocateResult.InvalidMarker(markerPath, markerError);
            }
        }

        if (markerPathResult.MarkerPaths.Count > 1)
        {
            return UnityUcliPluginLocateResult.MultipleFound(
                markerPathResult.MarkerPaths,
                ExecutionError.InvalidArgument(
                    $"Multiple uCLI Unity plugin markers were found. Paths={string.Join(", ", markerPathResult.MarkerPaths)}"));
        }

        pluginMarkerCacheCoordinator.WriteBestEffort(
            locateContext.UnityProjectRoot,
            locateContext.StorageRoot,
            locateContext.ProjectFingerprint,
            markerPathResult.MarkerPaths[0]);

        return UnityUcliPluginLocateResult.Found(
            markerPathResult.MarkerPaths[0],
            UnityUcliPluginMarkerContract.ExpectedProtocolVersion);
    }

    private static LocateContext CreateLocateContext (string unityProjectRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(unityProjectRoot);

        var projectRootResult = PathNormalizer.TryNormalizeFullPath(unityProjectRoot);
        if (!projectRootResult.IsSuccess)
        {
            throw new ArgumentException(projectRootResult.DiagnosticMessage, nameof(unityProjectRoot));
        }

        var normalizedUnityProjectRoot = projectRootResult.FullPath!;
        var storageRoot = UcliStoragePathResolver.ResolveStorageRoot(normalizedUnityProjectRoot);
        var projectFingerprint = UnityProjectFingerprintCalculator.Create(storageRoot, normalizedUnityProjectRoot);
        return new LocateContext(normalizedUnityProjectRoot, storageRoot, projectFingerprint);
    }

    /// <summary> Carries normalized storage identity used by marker cache lookup. </summary>
    private sealed record LocateContext (
        string UnityProjectRoot,
        string StorageRoot,
        ProjectFingerprint ProjectFingerprint);
}
