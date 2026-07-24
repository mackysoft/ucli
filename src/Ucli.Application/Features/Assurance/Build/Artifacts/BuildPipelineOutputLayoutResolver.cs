using System.Diagnostics.CodeAnalysis;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;

/// <summary> Resolves guarded BuildPipeline output layouts from product build-target policy. </summary>
internal static class BuildPipelineOutputLayoutResolver
{
    /// <summary>
    /// Attempts to resolve the output layout while keeping the output directory guarded across application boundaries.
    /// </summary>
    public static bool TryResolve (
        AbsolutePath outputDirectory,
        BuildTargetStableName buildTarget,
        bool androidAppBundle,
        [NotNullWhen(true)] out BuildPipelineOutputLayout? layout)
    {
        ArgumentNullException.ThrowIfNull(outputDirectory);

        layout = null;
        if (!BuildPipelineOutputLayoutPolicy.TryResolve(
                buildTarget,
                androidAppBundle,
                out var definition))
        {
            return false;
        }

        var location = ContainedPath.Create(
            outputDirectory,
            BuildRunnerOutputPathAdapter.ToRootRelativePath(definition.RunnerOutputPath));
        layout = new BuildPipelineOutputLayout(definition.Shape, location.Target);
        return true;
    }
}
