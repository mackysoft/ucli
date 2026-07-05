namespace MackySoft.Ucli.Tests;

using MackySoft.Tests;

public sealed class UnityOperationAuthoringContractTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public void OperationImplementations_DoNotReadRawJsonArgs ()
    {
        var operationsRoot = TestRepositoryPaths.GetFullPath(
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
                Path = TestRepositoryPaths.NormalizeRepositoryRelativePath(path),
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

}
