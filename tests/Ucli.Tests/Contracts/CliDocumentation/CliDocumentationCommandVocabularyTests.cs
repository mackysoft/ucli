namespace MackySoft.Ucli.Tests;

public sealed class CliDocumentationCommandVocabularyTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public void RepositoryArtifacts_DoNotReferenceErrorsCommand ()
    {
        string[] prohibitedCommandFragments =
        [
            "ucli errors",
            "errors describe",
            "errors list",
            "errors explain",
        ];
        var violations = EnumerateDocumentedArtifactPaths()
            .SelectMany(path => FindProhibitedCommandFragments(path, prohibitedCommandFragments))
            .ToArray();

        Assert.Empty(violations);
    }

    private static IEnumerable<string> EnumerateDocumentedArtifactPaths ()
    {
        var readmePath = TestRepositoryPaths.GetFullPath("README.md");
        if (File.Exists(readmePath))
        {
            yield return readmePath;
        }

        string[] roots =
        [
            TestRepositoryPaths.GetFullPath("skills", "definitions"),
            TestRepositoryPaths.GetFullPath("skills", "generated"),
        ];
        foreach (var root in roots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
            {
                yield return file;
            }
        }

        foreach (string file in CliOutputGoldenFiles.EnumerateFullPaths())
        {
            yield return file;
        }
    }

    private static IEnumerable<string> FindProhibitedCommandFragments (
        string path,
        IReadOnlyList<string> prohibitedCommandFragments)
    {
        var text = File.ReadAllText(path);
        for (var i = 0; i < prohibitedCommandFragments.Count; i++)
        {
            if (text.Contains(prohibitedCommandFragments[i], StringComparison.OrdinalIgnoreCase))
            {
                yield return $"{TestRepositoryPaths.NormalizeRepositoryRelativePath(path)} contains '{prohibitedCommandFragments[i]}'.";
            }
        }
    }
}
