using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;
using MackySoft.Ucli.Contracts.Assurance.Build;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Build;

public sealed class BuildRunArtifactAccountingRequestTests
{
    [Theory]
    [InlineData("paths")]
    [InlineData("buildTarget")]
    [InlineData("unityBuildTarget")]
    [InlineData("outputSources")]
    [Trait("Size", "Small")]
    public void Constructor_WhenRequiredInputIsInvalid_Throws (string parameterName)
    {
        var paths = CreatePaths();

        var exception = Assert.ThrowsAny<ArgumentException>(() => parameterName switch
        {
            "paths" => CreateRequest(null!, BuildTargetStableName.StandaloneLinux64, "StandaloneLinux64", []),
            "buildTarget" => CreateRequest(paths, default, "StandaloneLinux64", []),
            "unityBuildTarget" => CreateRequest(paths, BuildTargetStableName.StandaloneLinux64, " ", []),
            "outputSources" => CreateRequest(paths, BuildTargetStableName.StandaloneLinux64, "StandaloneLinux64", null!),
            _ => throw new ArgumentOutOfRangeException(nameof(parameterName), parameterName, null),
        });

        Assert.Equal(parameterName, exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenOutputSourcesContainNull_Throws ()
    {
        var exception = Assert.Throws<ArgumentException>(() => CreateRequest(
            CreatePaths(),
            BuildTargetStableName.StandaloneLinux64,
            "StandaloneLinux64",
            [null!]));

        Assert.Equal("outputSources", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenSourceCollectionChanges_PreservesConstructionSnapshot ()
    {
        var source = BuildOutputSourceEntry.FromAbsolutePath(AbsolutePath.Parse(
            Path.Combine(Path.GetTempPath(), "ucli-build-source", "player")));
        var sources = new List<BuildOutputSourceEntry> { source };
        var request = CreateRequest(
            CreatePaths(),
            BuildTargetStableName.StandaloneLinux64,
            "StandaloneLinux64",
            sources);

        sources.Clear();

        Assert.Equal([source], request.OutputSources);
    }

    private static BuildRunArtifactAccountingRequest CreateRequest (
        BuildRunArtifactPaths paths,
        BuildTargetStableName buildTarget,
        string unityBuildTarget,
        IReadOnlyList<BuildOutputSourceEntry> outputSources)
    {
        return new BuildRunArtifactAccountingRequest(
            paths,
            buildTarget,
            unityBuildTarget,
            buildReport: null,
            outputSources,
            allowEmptyOutputManifest: false);
    }

    private static BuildRunArtifactPaths CreatePaths ()
    {
        var root = Path.Combine(Path.GetTempPath(), "ucli-build-accounting-request-tests");
        var artifacts = Path.Combine(root, "artifacts");
        return new BuildRunArtifactPaths(
            AbsolutePath.Parse(root),
            Guid.Parse("00000000-0000-0000-0000-000000000452"),
            AbsolutePath.Parse(artifacts),
            AbsolutePath.Parse(Path.Combine(artifacts, "build.json")),
            AbsolutePath.Parse(Path.Combine(artifacts, "build-report.json")),
            AbsolutePath.Parse(Path.Combine(artifacts, "build.log")),
            AbsolutePath.Parse(Path.Combine(artifacts, "output-manifest.json")),
            AbsolutePath.Parse(Path.Combine(root, "runner-output")),
            AbsolutePath.Parse(Path.Combine(artifacts, "output")));
    }
}
