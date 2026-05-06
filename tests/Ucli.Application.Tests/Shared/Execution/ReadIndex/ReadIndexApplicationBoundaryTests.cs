namespace MackySoft.Ucli.Application.Tests.Execution.ReadIndex;

public sealed class ReadIndexApplicationBoundaryTests
{
    private static readonly string[] ForbiddenFragments =
    [
        "using System.IO;",
        "MackySoft.Ucli.Infrastructure",
        "MackySoft.Ucli.UnityIntegration",
        "new FileInfo",
        "new DirectoryInfo",
    ];

    private static readonly string[] ForbiddenPatterns =
    [
        @"\bFile\.",
        @"\bDirectory\.",
        @"\bPath\.",
    ];

    [Fact]
    [Trait("Size", "Small")]
    public void ApplicationProductionSources_DoNotReferenceFilesystemOrAdapterImplementations ()
    {
        var applicationSourceRoot = ResolveApplicationSourceRoot();
        var files = Directory.EnumerateFiles(applicationSourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(static path => IsProductionSourceFile(path))
            .ToArray();

        Assert.NotEmpty(files);

        foreach (var file in files)
        {
            var source = File.ReadAllText(file);
            foreach (var forbiddenFragment in ForbiddenFragments)
            {
                Assert.False(
                    source.Contains(forbiddenFragment, StringComparison.Ordinal),
                    $"{file} contains forbidden Application dependency fragment '{forbiddenFragment}'.");
            }

            foreach (var forbiddenPattern in ForbiddenPatterns)
            {
                Assert.False(
                    ContainsForbiddenPattern(source, forbiddenPattern),
                    $"{file} contains forbidden Application dependency pattern '{forbiddenPattern}'.");
            }
        }
    }

    private static string ResolveApplicationSourceRoot ()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            var candidate = Path.Combine(current.FullName, "src", "Ucli.Application");
            if (File.Exists(Path.Combine(candidate, "Ucli.Application.csproj")))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not resolve Ucli.Application source root.");
    }

    private static bool IsProductionSourceFile (string path)
    {
        return !path.EndsWith("AssemblyInfo.cs", StringComparison.Ordinal)
            && !ContainsDirectorySegment(path, "bin")
            && !ContainsDirectorySegment(path, "obj");
    }

    private static bool ContainsDirectorySegment (string path, string segment)
    {
        var marker = $"{Path.DirectorySeparatorChar}{segment}{Path.DirectorySeparatorChar}";
        return path.Contains(marker, StringComparison.Ordinal);
    }

    private static bool ContainsForbiddenPattern (string source, string pattern)
    {
        return System.Text.RegularExpressions.Regex.IsMatch(
            source,
            pattern,
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    }
}
