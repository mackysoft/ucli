using MackySoft.Ucli.Application.Shared.Foundation;
using static MackySoft.Ucli.Tests.UnityUcliPluginLocatorTestSupport;

namespace MackySoft.Ucli.Tests;

public sealed class UnityUcliPluginLocatorFailureTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Locate_WhenCacheIsMissingAndStandardAndNonStandardMarkersExist_ReturnsMultipleFound ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ucli-plugin-locator", "multiple-found-standard-and-nonstandard");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        await WriteMarkerAsync(scope, Path.Combine("UnityProject", "Assets", "MackySoft", "MackySoft.Ucli.Unity"));
        await WriteMarkerAsync(scope, Path.Combine("UnityProject", "Assets", "ThirdParty", "UcliCopy"));
        var locator = CreateLocator();

        var result = await locator.LocateAsync(unityProjectPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityUcliPluginLocateStatus.MultipleFound, result.Status);
        Assert.Equal(2, result.MarkerPaths.Count);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Locate_WhenMarkerDoesNotExist_ReturnsNotFound ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ucli-plugin-locator", "not-found");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var locator = CreateLocator();

        var result = await locator.LocateAsync(unityProjectPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityUcliPluginLocateStatus.NotFound, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("does not contain the uCLI Unity plugin", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Locate_WhenMarkerJsonIsInvalid_ReturnsInvalidMarker ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ucli-plugin-locator", "invalid-json");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        await scope.WriteFileAsync(
            Path.Combine("UnityProject", "Assets", "MackySoft", "MackySoft.Ucli.Unity", UnityUcliPluginMarkerContract.MarkerFileName),
            "{ invalid");
        var locator = CreateLocator();

        var result = await locator.LocateAsync(unityProjectPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityUcliPluginLocateStatus.InvalidMarker, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("could not be parsed", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Locate_WhenPluginIdDoesNotMatch_ReturnsInvalidMarker ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ucli-plugin-locator", "invalid-plugin-id");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        await WriteMarkerAsync(
            scope,
            Path.Combine("UnityProject", "Assets", "MackySoft", "MackySoft.Ucli.Unity"),
            """
            {
              "pluginId": "com.example.other",
              "protocolVersion": 1
            }
            """);
        var locator = CreateLocator();

        var result = await locator.LocateAsync(unityProjectPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityUcliPluginLocateStatus.InvalidMarker, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("pluginId must be", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Locate_WhenProtocolVersionDoesNotMatch_ReturnsInvalidMarker ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ucli-plugin-locator", "invalid-protocol-version");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        await WriteMarkerAsync(
            scope,
            Path.Combine("UnityProject", "Assets", "MackySoft", "MackySoft.Ucli.Unity"),
            """
            {
              "pluginId": "com.mackysoft.ucli.unity",
              "protocolVersion": 2
            }
            """);
        var locator = CreateLocator();

        var result = await locator.LocateAsync(unityProjectPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityUcliPluginLocateStatus.InvalidMarker, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("protocolVersion must be", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Locate_WhenMultipleMarkersExist_ReturnsMultipleFound ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ucli-plugin-locator", "multiple-found");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        await WriteMarkerAsync(scope, Path.Combine("UnityProject", "Assets", "MackySoft", "MackySoft.Ucli.Unity"));
        await WriteMarkerAsync(scope, Path.Combine("UnityProject", "Packages", "com.mackysoft.ucli.unity"));
        var locator = CreateLocator();

        var result = await locator.LocateAsync(unityProjectPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityUcliPluginLocateStatus.MultipleFound, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Equal(2, result.MarkerPaths.Count);
        Assert.Contains("Multiple uCLI Unity plugin markers were found", error.Message, StringComparison.Ordinal);
    }
}
