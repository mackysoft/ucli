namespace MackySoft.Ucli.Tests;

using System.Text.Json;
using System.Xml.Linq;
using MackySoft.FileSystem;
using MackySoft.Tests;
using MackySoft.Ucli.UnityIntegration.Project.Plugin;
using MackySoft.Ucli.UnityIntegration.Project.Plugin.Cache;
using MackySoft.Ucli.UnityIntegration.Project.Plugin.Marker;
using static MackySoft.Ucli.Tests.UnityUcliPluginLocatorTestSupport;

public sealed class UnityPluginPackageSpecTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public void UnityConsumer_ReferencesFileSystemPackageAssembly ()
    {
        string editorAssemblyDefinitionPath = TestRepositoryPaths.GetFullPath(
            "src",
            "Ucli.Unity",
            "Assets",
            "MackySoft",
            "MackySoft.Ucli.Unity",
            "Editor",
            "MackySoft.Ucli.Unity.Editor.asmdef");
        string testAssemblyDefinitionPath = TestRepositoryPaths.GetFullPath(
            "src",
            "Ucli.Unity",
            "Assets",
            "Tests",
            "MackySoft.Ucli.Unity.Tests",
            "Editor",
            "MackySoft.Ucli.Unity.Tests.Editor.asmdef");

        Assert.Contains(
            "MackySoft.FileSystem",
            ReadAssemblyDefinitionReferences(editorAssemblyDefinitionPath, "references"));
        Assert.Contains(
            "MackySoft.FileSystem.dll",
            ReadAssemblyDefinitionReferences(testAssemblyDefinitionPath, "precompiledReferences"));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void NuGetForUnity_MapsFileSystemPackageOnlyToPublicSource ()
    {
        XDocument document = XDocument.Load(TestRepositoryPaths.GetFullPath(
            "src",
            "Ucli.Unity",
            "Assets",
            "NuGet.config"));
        XElement publicSourceMapping = document
            .Descendants("packageSource")
            .Single(element => string.Equals(
                element.Attribute("key")?.Value,
                "nuget.org",
                StringComparison.Ordinal));
        Assert.Contains(
            publicSourceMapping.Elements("package"),
            static element => string.Equals(
                element.Attribute("pattern")?.Value,
                "MackySoft.FileSystem",
                StringComparison.Ordinal));

        Assert.Single(
            document
                .Descendants("packageSource")
                .SelectMany(static source => source.Elements("package")),
            static element => string.Equals(
                element.Attribute("pattern")?.Value,
                "MackySoft.FileSystem",
                StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Nuspec_DeclaresPinnedFileSystemDependencyAndSameRemainingDependenciesAsUnityPackagesConfig ()
    {
        var nuspecPath = TestRepositoryPaths.GetFullPath("src", "Ucli.Unity", "MackySoft.Ucli.Unity.nuspec");
        var packagesConfigPath = TestRepositoryPaths.GetFullPath("src", "Ucli.Unity", "Assets", "packages.config");

        var nuspecDependencies = ReadNuspecDependencies(nuspecPath);
        var packagesConfigDependencies = ReadPackagesConfigDependencies(packagesConfigPath);
        var expectedNuspecDependencies = packagesConfigDependencies.ToDictionary(
            static dependency => dependency.Key,
            static dependency => string.Equals(
                dependency.Key,
                "MackySoft.FileSystem",
                StringComparison.Ordinal)
                ? $"[{dependency.Value}]"
                : dependency.Value,
            StringComparer.Ordinal);

        Assert.Equal(expectedNuspecDependencies, nuspecDependencies);
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
        var unityProjectRoot = AbsolutePath.Parse(unityProjectPath);
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

        var result = await locator.LocateAsync(unityProjectRoot, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(UnityUcliPluginLocateStatus.Found, result.Status);
        Assert.Equal(
            ResolveMarkerPath(
                unityProjectRoot,
                RootRelativePath.Parse(Path.Combine("Assets", "Packages", $"MackySoft.Ucli.Unity.{packageVersion}"))),
            result.MarkerPath);
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

    private static string[] ReadAssemblyDefinitionReferences (
        string assemblyDefinitionPath,
        string propertyName)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(assemblyDefinitionPath));
        return document.RootElement
            .GetProperty(propertyName)
            .EnumerateArray()
            .Select(static element => element.GetString() ?? string.Empty)
            .ToArray();
    }

    private static IReadOnlyDictionary<string, string> ReadPackagesConfigDependencies (string packagesConfigPath)
    {
        var document = XDocument.Load(packagesConfigPath);
        return document
            .Descendants("package")
            .ToDictionary(
                package => package.Attribute("id")?.Value ?? string.Empty,
                package => package.Attribute("version")?.Value ?? string.Empty,
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
