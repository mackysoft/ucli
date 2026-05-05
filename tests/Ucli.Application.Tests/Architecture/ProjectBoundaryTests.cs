using System.Xml.Linq;

namespace MackySoft.Ucli.Application.Tests.Architecture;

public sealed class ProjectBoundaryTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    [Trait("Size", "Small")]
    public void ProductionProjects_reference_only_allowed_projects ()
    {
        var expectedReferencesByProject = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["src/Ucli.Application/Ucli.Application.csproj"] =
            [
                "src/Ucli.Contracts/Ucli.Contracts.csproj",
            ],
            ["src/Ucli.Contracts/Ucli.Contracts.csproj"] = [],
            ["src/Ucli.Infrastructure/Ucli.Infrastructure.csproj"] =
            [
                "src/Ucli.Contracts/Ucli.Contracts.csproj",
            ],
            ["src/Ucli.Skills/Ucli.Skills.csproj"] = [],
            ["src/Ucli/Ucli.csproj"] =
            [
                "src/Ucli.Application/Ucli.Application.csproj",
                "src/Ucli.Contracts/Ucli.Contracts.csproj",
                "src/Ucli.Infrastructure/Ucli.Infrastructure.csproj",
                "src/Ucli.Skills/Ucli.Skills.csproj",
            ],
        };

        var actualProjectPaths = Directory
            .EnumerateFiles(Path.Combine(RepositoryRoot, "src"), "*.csproj", SearchOption.AllDirectories)
            .Select(NormalizeRepositoryRelativePath)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            expectedReferencesByProject.Keys.OrderBy(static value => value, StringComparer.Ordinal),
            actualProjectPaths);

        foreach (var (projectPath, expectedReferences) in expectedReferencesByProject)
        {
            var actualReferences = ReadProjectReferences(projectPath);
            Assert.Equal(
                expectedReferences.OrderBy(static value => value, StringComparer.Ordinal),
                actualReferences.OrderBy(static value => value, StringComparer.Ordinal));
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TestProjects_reference_only_allowed_projects ()
    {
        var expectedReferencesByProject = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["tests/Tests.Helper/Tests.Helper.csproj"] = [],
            ["tests/Ucli.Application.Tests/Ucli.Application.Tests.csproj"] =
            [
                "src/Ucli.Application/Ucli.Application.csproj",
                "tests/Tests.Helper/Tests.Helper.csproj",
            ],
            ["tests/Ucli.Contracts.Tests/Ucli.Contracts.Tests.csproj"] =
            [
                "src/Ucli.Contracts/Ucli.Contracts.csproj",
                "tests/Tests.Helper/Tests.Helper.csproj",
            ],
            ["tests/Ucli.Infrastructure.Tests/Ucli.Infrastructure.Tests.csproj"] =
            [
                "src/Ucli.Contracts/Ucli.Contracts.csproj",
                "src/Ucli.Infrastructure/Ucli.Infrastructure.csproj",
                "tests/Tests.Helper/Tests.Helper.csproj",
            ],
            ["tests/Ucli.Skills.Tests/Ucli.Skills.Tests.csproj"] =
            [
                "src/Ucli.Skills/Ucli.Skills.csproj",
                "tests/Tests.Helper/Tests.Helper.csproj",
            ],
            ["tests/Ucli.Tests/Ucli.Tests.csproj"] =
            [
                "src/Ucli.Application/Ucli.Application.csproj",
                "src/Ucli.Contracts/Ucli.Contracts.csproj",
                "src/Ucli.Infrastructure/Ucli.Infrastructure.csproj",
                "src/Ucli/Ucli.csproj",
                "tests/Tests.Helper/Tests.Helper.csproj",
            ],
        };

        var actualProjectPaths = Directory
            .EnumerateFiles(Path.Combine(RepositoryRoot, "tests"), "*.csproj", SearchOption.AllDirectories)
            .Select(NormalizeRepositoryRelativePath)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            expectedReferencesByProject.Keys.OrderBy(static value => value, StringComparer.Ordinal),
            actualProjectPaths);

        foreach (var (projectPath, expectedReferences) in expectedReferencesByProject)
        {
            var actualReferences = ReadProjectReferences(projectPath);
            Assert.Equal(
                expectedReferences.OrderBy(static value => value, StringComparer.Ordinal),
                actualReferences.OrderBy(static value => value, StringComparer.Ordinal));
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ProductionProjects_use_only_allowed_packages ()
    {
        var expectedPackagesByProject = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["src/Ucli.Application/Ucli.Application.csproj"] =
            [
                "Microsoft.Extensions.DependencyInjection.Abstractions",
            ],
            ["src/Ucli.Contracts/Ucli.Contracts.csproj"] =
            [
                "System.Text.Json",
            ],
            ["src/Ucli.Infrastructure/Ucli.Infrastructure.csproj"] = [],
            ["src/Ucli.Skills/Ucli.Skills.csproj"] = [],
            ["src/Ucli/Ucli.csproj"] =
            [
                "ConsoleAppFramework",
                "Microsoft.Extensions.DependencyInjection",
            ],
        };

        foreach (var (projectPath, expectedPackages) in expectedPackagesByProject)
        {
            var actualPackages = ReadPackageReferences(projectPath);
            Assert.Equal(
                expectedPackages.OrderBy(static value => value, StringComparer.Ordinal),
                actualPackages.OrderBy(static value => value, StringComparer.Ordinal));
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Application_source_does_not_reference_host_or_adapter_namespaces ()
    {
        var forbiddenNamespaceMarkers = new[]
        {
            "MackySoft.Ucli.Hosting",
            "MackySoft.Ucli.Infrastructure",
            "MackySoft.Ucli.UnityIntegration",
        };

        var applicationSourceFiles = EnumerateCSharpSourceFiles("src/Ucli.Application");

        foreach (var sourceFile in applicationSourceFiles)
        {
            var sourceText = File.ReadAllText(sourceFile);
            foreach (var marker in forbiddenNamespaceMarkers)
            {
                Assert.DoesNotContain(marker, sourceText, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Application_source_does_not_use_host_resource_apis ()
    {
        var forbiddenSourceMarkers = new[]
        {
            "System.Diagnostics.Process",
            "DiagnosticsProcess",
            "Process.Start(",
            "new Process(",
            "File.",
            "Directory.",
            "Environment.",
            "System.Net.Sockets",
            "SocketException",
            "FileStream",
            "DirectoryInfo",
        };

        var applicationSourceFiles = EnumerateCSharpSourceFiles("src/Ucli.Application");

        foreach (var sourceFile in applicationSourceFiles)
        {
            var sourceText = File.ReadAllText(sourceFile);
            foreach (var marker in forbiddenSourceMarkers)
            {
                Assert.DoesNotContain(marker, sourceText, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Application_namespace_is_declared_only_by_application_project ()
    {
        var nonApplicationSourceFiles = EnumerateCSharpSourceFiles("src")
            .Where(sourceFile => !NormalizeRepositoryRelativePath(sourceFile).StartsWith("src/Ucli.Application/", StringComparison.Ordinal));

        foreach (var sourceFile in nonApplicationSourceFiles)
        {
            var sourceText = File.ReadAllText(sourceFile);
            Assert.DoesNotContain("namespace MackySoft.Ucli.Application", sourceText, StringComparison.Ordinal);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Application_tests_do_not_reference_host_or_adapter_namespaces ()
    {
        var forbiddenNamespaceMarkers = new[]
        {
            "MackySoft.Ucli.Hosting",
            "MackySoft.Ucli.Infrastructure",
            "MackySoft.Ucli.UnityIntegration",
            "MackySoft.Ucli.Features.",
            "MackySoft.Ucli.Tests",
        };

        var applicationTestFiles = EnumerateCSharpSourceFiles("tests/Ucli.Application.Tests")
            .Where(static sourceFile => !sourceFile.EndsWith("ProjectBoundaryTests.cs", StringComparison.Ordinal));

        foreach (var sourceFile in applicationTestFiles)
        {
            var sourceText = File.ReadAllText(sourceFile);
            foreach (var marker in forbiddenNamespaceMarkers)
            {
                Assert.DoesNotContain(marker, sourceText, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Feature_use_case_implementations_are_not_owned_by_cli_host_project ()
    {
        var hostFeatureFiles = Directory
            .EnumerateFiles(Path.Combine(RepositoryRoot, "src", "Ucli", "Features"), "*.cs", SearchOption.AllDirectories)
            .Select(NormalizeRepositoryRelativePath)
            .Where(static relativePath => relativePath.Contains("/UseCases/", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(hostFeatureFiles);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void InternalsVisibleTo_lists_are_boundary_explicit ()
    {
        var expectedFriendsByAssemblyInfo = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["src/Ucli.Application/AssemblyInfo.cs"] =
            [
                "MackySoft.Ucli",
                "MackySoft.Ucli.Application.Tests",
                "MackySoft.Ucli.Tests",
            ],
            ["src/Ucli.Contracts/AssemblyInfo.cs"] =
            [
                "MackySoft.Ucli",
                "MackySoft.Ucli.Application",
                "MackySoft.Ucli.Application.Tests",
                "MackySoft.Ucli.Contracts.Tests",
                "MackySoft.Ucli.Infrastructure",
                "MackySoft.Ucli.Infrastructure.Tests",
                "MackySoft.Ucli.Tests",
                "MackySoft.Ucli.Unity.Editor",
                "MackySoft.Ucli.Unity.Tests.Editor",
            ],
        };

        foreach (var (assemblyInfoPath, expectedFriends) in expectedFriendsByAssemblyInfo)
        {
            var actualFriends = ReadInternalsVisibleToAssemblyNames(assemblyInfoPath);
            Assert.Equal(
                expectedFriends.OrderBy(static value => value, StringComparer.Ordinal),
                actualFriends.OrderBy(static value => value, StringComparer.Ordinal));
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Unity_asmdefs_do_not_reference_cli_host_or_application_assemblies ()
    {
        var forbiddenAssemblyReferences = new[]
        {
            "\"MackySoft.Ucli\"",
            "\"MackySoft.Ucli.Application\"",
        };

        var asmdefFiles = Directory.EnumerateFiles(
            Path.Combine(RepositoryRoot, "src", "Ucli.Unity"),
            "*.asmdef",
            SearchOption.AllDirectories);

        foreach (var asmdefFile in asmdefFiles)
        {
            var asmdefText = File.ReadAllText(asmdefFile);
            foreach (var marker in forbiddenAssemblyReferences)
            {
                Assert.DoesNotContain(marker, asmdefText, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Unity_plugin_source_does_not_reference_cli_host_or_application_boundaries ()
    {
        var forbiddenMarkers = new[]
        {
            "MackySoft.Ucli.Application",
            "MackySoft.Ucli.Hosting",
            "MackySoft.Ucli.Features.",
        };

        var unitySourceFiles = EnumerateCSharpSourceFiles("src/Ucli.Unity");

        foreach (var sourceFile in unitySourceFiles)
        {
            var sourceText = File.ReadAllText(sourceFile);
            foreach (var marker in forbiddenMarkers)
            {
                Assert.DoesNotContain(marker, sourceText, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Skills_project_does_not_reference_application_or_unity_boundaries ()
    {
        var forbiddenMarkers = new[]
        {
            "MackySoft.Ucli.Application",
            "MackySoft.Ucli.Infrastructure",
            "UnityEngine",
        };

        var skillSourceFiles = EnumerateCSharpSourceFiles("src/Ucli.Skills");

        foreach (var sourceFile in skillSourceFiles)
        {
            var sourceText = File.ReadAllText(sourceFile);
            foreach (var marker in forbiddenMarkers)
            {
                Assert.DoesNotContain(marker, sourceText, StringComparison.Ordinal);
            }
        }
    }

    private static IEnumerable<string> EnumerateCSharpSourceFiles (string repositoryRelativeDirectory)
    {
        return Directory
            .EnumerateFiles(Path.Combine(RepositoryRoot, repositoryRelativeDirectory), "*.cs", SearchOption.AllDirectories)
            .Where(static sourceFile =>
            {
                var relativePath = NormalizeRepositoryRelativePath(sourceFile);
                return !relativePath.Contains("/bin/", StringComparison.Ordinal)
                    && !relativePath.Contains("/obj/", StringComparison.Ordinal);
            });
    }

    private static string[] ReadProjectReferences (string projectPath)
    {
        var projectFullPath = Path.Combine(RepositoryRoot, projectPath);
        var projectDirectory = Path.GetDirectoryName(projectFullPath)
            ?? throw new InvalidOperationException($"Project path has no directory: {projectFullPath}");
        var document = XDocument.Load(projectFullPath);
        return document
            .Descendants("ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(value => NormalizeRepositoryRelativePath(Path.GetFullPath(Path.Combine(projectDirectory, value!))))
            .ToArray();
    }

    private static string[] ReadPackageReferences (string projectPath)
    {
        var projectFullPath = Path.Combine(RepositoryRoot, projectPath);
        var document = XDocument.Load(projectFullPath);
        return document
            .Descendants("PackageReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value!)
            .ToArray();
    }

    private static string[] ReadInternalsVisibleToAssemblyNames (string assemblyInfoPath)
    {
        var sourceText = File.ReadAllText(Path.Combine(RepositoryRoot, assemblyInfoPath));
        const string marker = "InternalsVisibleTo(\"";
        var friends = new List<string>();
        var searchIndex = 0;
        while (true)
        {
            var markerIndex = sourceText.IndexOf(marker, searchIndex, StringComparison.Ordinal);
            if (markerIndex < 0)
            {
                return friends.ToArray();
            }

            var valueStart = markerIndex + marker.Length;
            var valueEnd = sourceText.IndexOf('"', valueStart);
            if (valueEnd < 0)
            {
                throw new InvalidOperationException($"Invalid InternalsVisibleTo declaration in {assemblyInfoPath}.");
            }

            friends.Add(sourceText[valueStart..valueEnd]);
            searchIndex = valueEnd + 1;
        }
    }

    private static string NormalizeRepositoryRelativePath (string fullPath)
    {
        return Path.GetRelativePath(RepositoryRoot, fullPath).Replace('\\', '/');
    }

    private static string FindRepositoryRoot ()
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDirectory != null)
        {
            if (File.Exists(Path.Combine(currentDirectory.FullName, "Ucli.slnx")))
            {
                return currentDirectory.FullName;
            }

            currentDirectory = currentDirectory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root from test output directory.");
    }
}
