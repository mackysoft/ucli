using System.Diagnostics.CodeAnalysis;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;

/// <summary>
/// Carries a BuildPipeline output shape and a structurally validated absolute output location.
/// </summary>
internal sealed class BuildPipelineOutputLayout
{
    internal BuildPipelineOutputLayout (
        IpcBuildOutputLayoutShape shape,
        AbsolutePath locationPath)
    {
        Shape = shape;
        LocationPath = locationPath ?? throw new ArgumentNullException(nameof(locationPath));
    }

    /// <summary> Gets the expected filesystem node shape of the BuildPipeline output. </summary>
    public IpcBuildOutputLayoutShape Shape { get; }

    /// <summary> Gets the normalized absolute BuildPipeline output location. </summary>
    public AbsolutePath LocationPath { get; }

    /// <summary>
    /// Attempts to convert one IPC layout at the transport boundary without accessing the filesystem.
    /// </summary>
    /// <returns>
    /// <see langword="true" /> when the location is an absolute path on the current platform;
    /// otherwise <see langword="false" /> and <paramref name="layout" /> is <see langword="null" />.
    /// </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="contract" /> is <see langword="null" />. </exception>
    public static bool TryFromContract (
        IpcBuildOutputLayout contract,
        [NotNullWhen(true)] out BuildPipelineOutputLayout? layout,
        out PathValidationFailure failure)
    {
        ArgumentNullException.ThrowIfNull(contract);

        layout = null;
        if (!AbsolutePath.TryParse(contract.LocationPathName, out var locationPath, out failure))
        {
            return false;
        }

        layout = new BuildPipelineOutputLayout(contract.Shape, locationPath);
        failure = default;
        return true;
    }

    /// <summary> Converts this guarded layout to its IPC representation. </summary>
    public IpcBuildOutputLayout ToContract ()
    {
        return new IpcBuildOutputLayout(Shape, LocationPath.Value);
    }
}
