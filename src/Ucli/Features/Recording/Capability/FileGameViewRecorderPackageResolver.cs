using System.Text.Json;
using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Recording.Capability;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Contracts.Recording;

namespace MackySoft.Ucli.Features.Recording.Capability;

/// <summary>Reads Recorder identity from Unity's resolved <c>Packages/packages-lock.json</c>.</summary>
internal sealed class FileGameViewRecorderPackageResolver : IGameViewRecorderPackageResolver
{
    private const int MaximumPackagesLockBytes = 4 * 1024 * 1024;
    private static readonly RootRelativePath PackagesLockRelativePath =
        RootRelativePath.Parse("Packages/packages-lock.json");

    public async ValueTask<GameViewRecorderPackageResolution> ResolveAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        cancellationToken.ThrowIfCancellationRequested();

        var packagesLockPath = ContainedPath.Create(
            unityProject.UnityProjectRoot,
            PackagesLockRelativePath).Target;
        ReadOnlyMemory<byte> bytes;
        try
        {
            var contents = await FileUtilities.ReadBytesOrNullWithinLimitAsync(
                    packagesLockPath,
                    MaximumPackagesLockBytes,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!contents.HasValue)
            {
                return GameViewRecorderPackageResolution.Indeterminate(
                    "Unity's resolved package lock file was not found.");
            }

            if (contents.Value.IsEmpty)
            {
                return GameViewRecorderPackageResolution.Indeterminate(
                    "Unity's resolved package lock file has an unsupported size.");
            }

            bytes = contents.Value;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return GameViewRecorderPackageResolution.Indeterminate(
                $"Unity's resolved package lock file could not be read. {exception.Message}");
        }

        try
        {
            using var document = JsonDocument.Parse(bytes);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("dependencies", out var dependencies)
                || dependencies.ValueKind != JsonValueKind.Object)
            {
                return GameViewRecorderPackageResolution.Indeterminate(
                    "Unity's resolved package lock file does not contain a dependency object.");
            }

            if (!dependencies.TryGetProperty(
                    GameViewRecorderCompatibilityMetadata.PackageId,
                    out var recorder))
            {
                return GameViewRecorderPackageResolution.Missing();
            }

            if (recorder.ValueKind != JsonValueKind.Object
                || !recorder.TryGetProperty("version", out var versionElement)
                || versionElement.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(versionElement.GetString()))
            {
                return GameViewRecorderPackageResolution.Indeterminate(
                    "Unity's resolved Recorder package entry has no version.");
            }

            return GameViewRecorderPackageResolution.Resolved(versionElement.GetString()!);
        }
        catch (JsonException exception)
        {
            return GameViewRecorderPackageResolution.Indeterminate(
                $"Unity's resolved package lock file is invalid JSON. {exception.Message}");
        }
    }
}
