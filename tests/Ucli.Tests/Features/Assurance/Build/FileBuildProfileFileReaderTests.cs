using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Features.Assurance.Build;

namespace MackySoft.Ucli.Tests.Features.Assurance.Build;

public sealed class FileBuildProfileFileReaderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAsync_WithRepositoryRelativeProfilePath_ReturnsJsonAndFullPath ()
    {
        using var repository = TestDirectories.CreateTempScope("ucli-build", nameof(ReadAsync_WithRepositoryRelativeProfilePath_ReturnsJsonAndFullPath));
        var profilePath = repository.WriteFile("profiles/build.json", """{"schemaVersion":1}""");
        var reader = new FileBuildProfileFileReader();

        var result = await reader.ReadAsync("profiles/build.json", CreateProject(repository.FullPath));

        Assert.True(result.IsSuccess);
        Assert.Equal("""{"schemaVersion":1}""", result.Json);
        Assert.Equal(profilePath, result.DisplayPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAsync_WithBlankProfilePath_ThrowsArgumentException ()
    {
        using var repository = TestDirectories.CreateTempScope("ucli-build", nameof(ReadAsync_WithBlankProfilePath_ThrowsArgumentException));
        var reader = new FileBuildProfileFileReader();

        await Assert.ThrowsAsync<ArgumentException>(
            () => reader.ReadAsync(" ", CreateProject(repository.FullPath)).AsTask());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAsync_WithMissingProfileFile_ReturnsBuildProfileInvalid ()
    {
        using var repository = TestDirectories.CreateTempScope("ucli-build", nameof(ReadAsync_WithMissingProfileFile_ReturnsBuildProfileInvalid));
        var reader = new FileBuildProfileFileReader();

        var result = await reader.ReadAsync("profiles/missing.json", CreateProject(repository.FullPath));

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, result.Error!.Kind);
        Assert.Equal(BuildErrorCodes.BuildProfileInvalid, result.Error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAsync_WithDirectoryPath_ReturnsBuildProfileInvalid ()
    {
        using var repository = TestDirectories.CreateTempScope("ucli-build", nameof(ReadAsync_WithDirectoryPath_ReturnsBuildProfileInvalid));
        repository.CreateDirectory("profiles");
        var reader = new FileBuildProfileFileReader();

        var result = await reader.ReadAsync("profiles", CreateProject(repository.FullPath));

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, result.Error!.Kind);
        Assert.Equal(BuildErrorCodes.BuildProfileInvalid, result.Error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAsync_WithOversizedProfileFile_ReturnsBuildProfileInvalid ()
    {
        using var repository = TestDirectories.CreateTempScope("ucli-build", nameof(ReadAsync_WithOversizedProfileFile_ReturnsBuildProfileInvalid));
        repository.WriteFile("profiles/build.json", new string('x', (1024 * 1024) + 1));
        var reader = new FileBuildProfileFileReader();

        var result = await reader.ReadAsync("profiles/build.json", CreateProject(repository.FullPath));

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, result.Error!.Kind);
        Assert.Equal(BuildErrorCodes.BuildProfileInvalid, result.Error.Code);
    }

    private static ResolvedUnityProjectContext CreateProject (string repositoryRoot)
    {
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: Path.Combine(repositoryRoot, "UnityProject"),
            RepositoryRoot: repositoryRoot,
            ProjectFingerprint: "project-fingerprint",
            PathSource: UnityProjectPathSource.CommandOption,
            PathSourceLabel: "--projectPath",
            UnityVersion: "6000.1.4f1");
    }
}
