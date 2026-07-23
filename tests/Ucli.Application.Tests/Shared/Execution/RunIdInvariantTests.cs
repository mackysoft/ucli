using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;
using MackySoft.Ucli.Application.Features.Assurance.Build.Payload;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Payload;
using MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;
using MackySoft.Ucli.Contracts.Testing;

namespace MackySoft.Ucli.Application.Tests.Shared.Execution;

public sealed class RunIdInvariantTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void CompileRequestPayload_WhenRunIdIsEmpty_ThrowsArgumentException ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new UnityRequestPayload.Compile(Guid.Empty));

        Assert.Equal("runId", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TestRunRequestPayload_WhenRunIdIsEmpty_ThrowsArgumentException ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new UnityRequestPayload.TestRun(
            TestRunPlatform.EditMode,
            null,
            null!,
            null!,
            false,
            Guid.Empty));

        Assert.Equal("runId", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TestRunRequestPayload_SnapshotsFilterCollections ()
    {
        var testCategories = new[] { "smoke" };
        var assemblyNames = new[] { "Game.Tests" };

        var payload = new UnityRequestPayload.TestRun(
            TestRunPlatform.EditMode,
            testFilter: null,
            testCategories,
            assemblyNames,
            failFast: false,
            Guid.NewGuid());

        testCategories[0] = "mutated-category";
        assemblyNames[0] = "Mutated.Tests";

        Assert.Equal("smoke", Assert.Single(payload.TestCategories));
        Assert.Equal("Game.Tests", Assert.Single(payload.AssemblyNames));
        Assert.Throws<NotSupportedException>(() => ((IList<string>)payload.TestCategories)[0] = "mutated-category");
        Assert.Throws<NotSupportedException>(() => ((IList<string>)payload.AssemblyNames)[0] = "Mutated.Tests");
    }

    [Theory]
    [InlineData(true, " ")]
    [InlineData(false, "")]
    [Trait("Size", "Small")]
    public void TestRunRequestPayload_WhenFilterValueIsBlank_RejectsInvalidValue (
        bool category,
        string invalidValue)
    {
        var exception = Assert.Throws<ArgumentException>(() => new UnityRequestPayload.TestRun(
            TestRunPlatform.EditMode,
            testFilter: null,
            testCategories: category ? [invalidValue] : [],
            assemblyNames: category ? [] : [invalidValue],
            failFast: false,
            Guid.NewGuid()));

        Assert.Equal(category ? "testCategories" : "assemblyNames", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void BuildRunArtifactPaths_WhenRunIdIsEmpty_ThrowsArgumentException ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new BuildRunArtifactPaths(
            null!,
            Guid.Empty,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!));

        Assert.Equal("runId", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void BuildRunMetadataDocument_WhenRunIdIsEmpty_ThrowsArgumentException ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new BuildRunMetadataDocument(
            1,
            Guid.Empty,
            default,
            default,
            default,
            default,
            default,
            default,
            default,
            default,
            default));

        Assert.Equal("runId", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void BuildOutput_WhenRunIdIsEmpty_ThrowsArgumentException ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new BuildOutput(
            Guid.Empty,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!));

        Assert.Equal("runId", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CompileOutput_WhenRunIdIsEmpty_ThrowsArgumentException ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new CompileOutput(
            Guid.Empty,
            null!,
            null!,
            null!,
            null!));

        Assert.Equal("runId", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ArtifactsSession_WhenRunIdIsEmpty_ThrowsArgumentException ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new ArtifactsSession(
            Guid.Empty,
            null!,
            default));

        Assert.Equal("runId", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ArtifactsSession_WhenStartedAtUtcIsNotCanonicalUtc_ThrowsArgumentException ()
    {
        var artifactRoot = AbsolutePath.Parse(Path.Combine(Path.GetTempPath(), "ucli-run-id-invariant"));
        var paths = new ArtifactPaths(
            artifactRoot,
            AbsolutePath.Resolve(artifactRoot, "meta"),
            AbsolutePath.Resolve(artifactRoot, "xml"),
            AbsolutePath.Resolve(artifactRoot, "log"),
            AbsolutePath.Resolve(artifactRoot, "results"),
            AbsolutePath.Resolve(artifactRoot, "summary"));
        var exception = Assert.Throws<ArgumentException>(() => new ArtifactsSession(
            Guid.NewGuid(),
            paths,
            new DateTimeOffset(2026, 7, 15, 9, 0, 0, TimeSpan.FromHours(9))));

        Assert.Equal("startedAtUtc", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TestRunServiceResult_WhenRunIdIsEmpty_ThrowsArgumentException ()
    {
        var artifactsDirectory = AbsolutePath.Parse(Path.Combine(
            Path.GetTempPath(),
            "ucli-run-id-invariant",
            "artifacts"));
        var exception = Assert.Throws<ArgumentException>(() => TestRunServiceResult.Pass(
            "Tests passed.",
            Guid.Empty,
            artifactsDirectory,
            AbsolutePath.Resolve(artifactsDirectory, "summary.json")));

        Assert.Equal("runId", exception.ParamName);
    }
}
