using MackySoft.Ucli.Application.Features.Recording.Capability;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Features.Recording.Capability;
using MackySoft.Tests;

namespace MackySoft.Ucli.Tests.Features.Recording.Capability;

public sealed class FileGameViewRecorderPackageResolverTests
{
    [Fact]
    public async Task ResolveAsync_WhenRecorderIsResolved_ReturnsItsVersion ()
    {
        using var directory = TestDirectories.CreateTempScope(
            "game-view-recorder-package",
            "resolved");
        var project = CreateProject(directory, """
            {
              "dependencies": {
                "com.unity.recorder": {
                  "version": "5.1.5",
                  "depth": 0,
                  "source": "registry"
                }
              }
            }
            """);

        var result = await new FileGameViewRecorderPackageResolver()
            .ResolveAsync(project);

        Assert.Equal(GameViewRecorderPackageResolutionState.Resolved, result.State);
        Assert.Equal("5.1.5", result.Version);
        Assert.Null(result.Diagnostic);
    }

    [Fact]
    public async Task ResolveAsync_WhenRecorderIsAbsent_ReturnsMissing ()
    {
        using var directory = TestDirectories.CreateTempScope(
            "game-view-recorder-package",
            "missing");
        var project = CreateProject(directory, "{\"dependencies\":{}}");

        var result = await new FileGameViewRecorderPackageResolver()
            .ResolveAsync(project);

        Assert.Equal(GameViewRecorderPackageResolutionState.Missing, result.State);
        Assert.Null(result.Version);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("{\"dependencies\":[]}")]
    [InlineData("{\"dependencies\":{")]
    public async Task ResolveAsync_WhenResolvedGraphCannotBeEstablished_ReturnsIndeterminate (
        string? packagesLockJson)
    {
        using var directory = TestDirectories.CreateTempScope(
            "game-view-recorder-package",
            "indeterminate");
        var project = CreateProject(directory, packagesLockJson);

        var result = await new FileGameViewRecorderPackageResolver()
            .ResolveAsync(project);

        Assert.Equal(GameViewRecorderPackageResolutionState.Indeterminate, result.State);
        Assert.Null(result.Version);
        Assert.False(string.IsNullOrWhiteSpace(result.Diagnostic));
    }

    private static ResolvedUnityProjectContext CreateProject (
        TestDirectoryScope directory,
        string? packagesLockJson)
    {
        var projectRoot = Path.Combine(directory.FullPath, "UnityProject");
        if (packagesLockJson is not null)
        {
            var packagesDirectory = Path.Combine(projectRoot, "Packages");
            Directory.CreateDirectory(packagesDirectory);
            File.WriteAllText(
                Path.Combine(packagesDirectory, "packages-lock.json"),
                packagesLockJson);
        }

        return ResolvedUnityProjectContextTestFactory.CreateWithPaths(
            unityProjectRoot: projectRoot,
            repositoryRoot: directory.FullPath,
            pathSource: UnityProjectPathSource.CommandOption,
            pathSourceLabel: projectRoot,
            unityVersion: "6000.3.11f1");
    }
}
