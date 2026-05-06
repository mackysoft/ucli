using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace MackySoft.Ucli.Architecture.Tests.Architecture;

internal static class ArchitectureTestRepository
{
    internal static string RepositoryRoot { get; } = FindRepositoryRoot();

    internal static string ToFullPath (string repositoryRelativePath)
    {
        return Path.Combine(RepositoryRoot, repositoryRelativePath);
    }

    internal static IEnumerable<string> EnumerateCSharpSourceFiles (string repositoryRelativeDirectory)
    {
        return Directory
            .EnumerateFiles(ToFullPath(repositoryRelativeDirectory), "*.cs", SearchOption.AllDirectories)
            .Where(static sourceFile =>
            {
                var relativePath = NormalizeRepositoryRelativePath(sourceFile);
                return !relativePath.Contains("/bin/", StringComparison.Ordinal)
                    && !relativePath.Contains("/obj/", StringComparison.Ordinal)
                    && !relativePath.Contains("/Library/", StringComparison.Ordinal)
                    && !relativePath.Contains("/Temp/", StringComparison.Ordinal);
            });
    }

    internal static IEnumerable<string> EnumerateProductionProjectFiles ()
    {
        return Directory
            .EnumerateFiles(ToFullPath("src"), "*.csproj", SearchOption.AllDirectories)
            .Select(NormalizeRepositoryRelativePath)
            .Where(static relativePath => !IsUnityGeneratedProjectFile(relativePath));
    }

    internal static IEnumerable<string> EnumerateTestProjectFiles ()
    {
        return Directory
            .EnumerateFiles(ToFullPath("tests"), "*.csproj", SearchOption.AllDirectories)
            .Select(NormalizeRepositoryRelativePath);
    }

    internal static IEnumerable<string> EnumerateRepositoryAssemblyInfoFiles ()
    {
        var ownedAssemblyInfoRoots = new[]
        {
            "src/Ucli.Application",
            "src/Ucli.Contracts",
            "src/Ucli.Infrastructure",
            "src/Ucli/Hosting",
            "src/Ucli.Unity/Assets/MackySoft/MackySoft.Ucli.Unity",
            "tests/Tests.Helper",
        };

        return ownedAssemblyInfoRoots
            .Select(ToFullPath)
            .SelectMany(static root => Directory.EnumerateFiles(root, "AssemblyInfo.cs", SearchOption.AllDirectories))
            .Select(NormalizeRepositoryRelativePath)
            .Where(static relativePath => !relativePath.Contains("/bin/", StringComparison.Ordinal)
                                          && !relativePath.Contains("/obj/", StringComparison.Ordinal));
    }

    internal static string NormalizeRepositoryRelativePath (string fullPath)
    {
        return Path.GetRelativePath(RepositoryRoot, fullPath).Replace('\\', '/');
    }

    internal static string ReadCSharpSourceWithoutCommentsAndStringLiterals (string sourceFile)
    {
        return CSharpSourceScanner.StripCommentsAndStringLiterals(File.ReadAllText(sourceFile));
    }

    internal static string[] ReadProjectReferences (string projectPath)
    {
        return ProjectFileReferenceReader.ReadProjectReferences(projectPath);
    }

    internal static string[] ReadPackageReferences (string projectPath)
    {
        return ProjectFileReferenceReader.ReadPackageReferences(projectPath);
    }

    internal static string[] ReadUnityAsmdefReferences (string asmdefPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(ToFullPath(asmdefPath)));
        if (!document.RootElement.TryGetProperty("references", out var references)
            || references.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return references
            .EnumerateArray()
            .Where(static element => element.ValueKind == JsonValueKind.String)
            .Select(static element => element.GetString())
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value!)
            .ToArray();
    }

    internal static string[] ReadUnityPackageIds (string packagesConfigPath)
    {
        var document = XDocument.Load(ToFullPath(packagesConfigPath));
        return document
            .Descendants("package")
            .Select(element => element.Attribute("id")?.Value)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value!)
            .ToArray();
    }

    internal static IEnumerable<string> ReadConcreteTypeNames (string sourceFile)
    {
        var sourceText = ReadCSharpSourceWithoutCommentsAndStringLiterals(sourceFile);
        return Regex
            .Matches(
                sourceText,
                @"\b(?:class|struct)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\b|\brecord\s+(?:class\s+|struct\s+)?(?<name>[A-Za-z_][A-Za-z0-9_]*)\b")
            .Select(static match => match.Groups["name"].Value)
            .Where(static value => !string.IsNullOrWhiteSpace(value));
    }

    internal static string[] ReadInternalsVisibleToAssemblyNames (string assemblyInfoPath)
    {
        var sourceText = File.ReadAllText(ToFullPath(assemblyInfoPath));
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

    internal static IEnumerable<PublicSurfaceDeclaration> ReadPublicSurfaceDeclarations (string sourceFile)
    {
        return PublicSurfaceDeclarationExtractor.Read(sourceFile);
    }

    private static bool IsUnityGeneratedProjectFile (string relativePath)
    {
        return relativePath.StartsWith("src/Ucli.Unity/", StringComparison.Ordinal)
            && relativePath.EndsWith(".csproj", StringComparison.Ordinal);
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
