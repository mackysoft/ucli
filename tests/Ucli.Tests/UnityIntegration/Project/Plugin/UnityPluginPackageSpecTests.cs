namespace MackySoft.Ucli.Tests;

using System.Xml.Linq;
using MackySoft.Tests;
using MackySoft.Ucli.UnityIntegration.Project.Plugin;
using MackySoft.Ucli.UnityIntegration.Project.Plugin.Cache;
using MackySoft.Ucli.UnityIntegration.Project.Plugin.Marker;

public sealed class UnityPluginPackageSpecTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public void Nuspec_DeclaresSameDependenciesAsUnityPackagesConfig ()
    {
        var nuspecPath = TestRepositoryPaths.GetFullPath("src", "Ucli.Unity", "MackySoft.Ucli.Unity.nuspec");
        var packagesConfigPath = TestRepositoryPaths.GetFullPath("src", "Ucli.Unity", "Assets", "packages.config");

        var nuspecDependencies = ReadNuspecDependencies(nuspecPath);
        var packagesConfigDependencies = ReadPackagesConfigDependencies(packagesConfigPath);

        Assert.Equal(packagesConfigDependencies, nuspecDependencies);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Locate_WhenNuGetForUnityRestoredUnityPackageContainsMarker_ReturnsFound ()
    {
        var packageVersion = ReadUnityPackageVersion(TestRepositoryPaths.GetFullPath(
            "src",
            "Ucli.Unity",
            "MackySoft.Ucli.Unity.nuspec"));
        using var scope = TestDirectories.CreateTempScope("unity-ucli-plugin-package", "nugetforunity-restore");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var packageRoot = Path.Combine("UnityProject", "Assets", "Packages", $"MackySoft.Ucli.Unity.{packageVersion}");
        scope.WriteFile(
            Path.Combine(packageRoot, UnityUcliPluginMarkerContract.MarkerFileName),
            """
            {
              "pluginId": "com.mackysoft.ucli.unity",
              "protocolVersion": 1
            }
            """);
        var locator = CreateLocator(CreateNoOpCacheStore());

        var result = await locator.LocateAsync(unityProjectPath, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(UnityUcliPluginLocateStatus.Found, result.Status);
        Assert.NotNull(result.MarkerPath);
        Assert.EndsWith(
            Path.Combine("Assets", "Packages", $"MackySoft.Ucli.Unity.{packageVersion}", UnityUcliPluginMarkerContract.MarkerFileName),
            result.MarkerPath,
            StringComparison.Ordinal);
    }

    private static IReadOnlyDictionary<string, string> ReadNuspecDependencies (string nuspecPath)
    {
        var document = XDocument.Load(nuspecPath);
        var ns = document.Root?.Name.Namespace ?? XNamespace.None;
        return document
            .Descendants(ns + "dependency")
            .ToDictionary(
                dependency => dependency.Attribute("id")?.Value ?? string.Empty,
                dependency => dependency.Attribute("version")?.Value ?? string.Empty,
                StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, string> ReadPackagesConfigDependencies (string packagesConfigPath)
    {
        var document = XDocument.Load(packagesConfigPath);
        return document
            .Descendants("package")
            .ToDictionary(
                package => package.Attribute("id")?.Value ?? string.Empty,
                package =>
                {
                    string id = package.Attribute("id")?.Value ?? string.Empty;
                    string version = package.Attribute("version")?.Value ?? string.Empty;
                    return id == "MackySoft.Json.Canonicalization"
                        ? $"[{version}]"
                        : version;
                },
                StringComparer.Ordinal);
    }

    private static string ReadUnityPackageVersion (string nuspecPath)
    {
        var document = XDocument.Load(nuspecPath);
        var ns = document.Root?.Name.Namespace ?? XNamespace.None;
        return document
            .Descendants(ns + "version")
            .Select(element => element.Value)
            .First();
    }

    private static UnityUcliPluginMarkerCacheStore CreateNoOpCacheStore ()
    {
        return new UnityUcliPluginMarkerCacheStore(
            static (_, _) => ValueTask.FromResult<string?>(null),
            static (_, _, _) => ValueTask.CompletedTask,
            static _ => { });
    }

    private static UnityUcliPluginLocator CreateLocator (UnityUcliPluginMarkerCacheStore cacheStore)
    {
        var markerValidator = new UnityUcliPluginMarkerValidator();
        return new UnityUcliPluginLocator(
            new UnityUcliPluginMarkerDiscovery(),
            markerValidator,
            new UnityUcliPluginMarkerCacheCoordinator(cacheStore, markerValidator));
    }

}
