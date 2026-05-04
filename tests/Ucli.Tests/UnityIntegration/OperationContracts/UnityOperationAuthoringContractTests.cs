namespace MackySoft.Ucli.Tests;

public sealed class UnityOperationAuthoringContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void OperationImplementations_DoNotReadRawJsonArgs ()
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var operationsRoot = Path.Combine(
            repositoryRoot,
            "src",
            "Ucli.Unity",
            "Assets",
            "MackySoft",
            "MackySoft.Ucli.Unity",
            "Editor",
            "Execution",
            "Phases",
            "Ops");

        var operationFiles = Directory.GetFiles(operationsRoot, "*Operation.cs", SearchOption.AllDirectories);
        Assert.NotEmpty(operationFiles);

        var offenders = operationFiles
            .Select(path => new
            {
                Path = Path.GetRelativePath(repositoryRoot, path),
                Text = File.ReadAllText(path),
            })
            .Where(file =>
                file.Text.Contains("operation.Args", StringComparison.Ordinal)
                || file.Text.Contains("JsonElement", StringComparison.Ordinal)
                || file.Text.Contains("TryGetProperty", StringComparison.Ordinal))
            .Select(file => file.Path)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    private static string ResolveRepositoryRoot ()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Ucli.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root could not be resolved from the test output directory.");
    }
}
