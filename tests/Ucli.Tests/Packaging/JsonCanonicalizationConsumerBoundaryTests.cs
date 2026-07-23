namespace MackySoft.Ucli.Tests.Packaging;

public sealed class JsonCanonicalizationConsumerBoundaryTests
{
    private static readonly string[] Rfc8785ConsumerPaths =
    [
        "src/Ucli.Application/Features/Assurance/Build/Profiles/BuildProfileDigestCalculator.cs",
        "src/Ucli.Application/Features/Assurance/Verify/Profiles/VerifyProfileDigestCalculator.cs",
    ];

    private static readonly string[] ProductSpecificExecutionIdentityPaths =
    [
        "src/Ucli.Unity/Assets/MackySoft/MackySoft.Ucli.Unity/Editor/Execution/Phases/Compilation/CompiledExecutionDigestWriter.cs",
        "src/Ucli.Unity/Assets/MackySoft/MackySoft.Ucli.Unity/Editor/Execution/RequestIdempotency/ExecuteRequestFingerprintCalculator.cs",
        "src/Ucli.Unity/Assets/MackySoft/MackySoft.Ucli.Unity/Editor/Execution/Requests/CanonicalRequestWriter.cs",
    ];

    private const string LegacyNumberSerializerImplementationNamespace = "Org.Webpki.Es6NumberSerialization";

    [Fact]
    [Trait("Size", "Medium")]
    public void Product_canonicalization_consumers_use_the_shared_RFC_8785_API ()
    {
        foreach (string relativePath in Rfc8785ConsumerPaths)
        {
            string contents = File.ReadAllText(TestRepositoryPaths.GetFullPath(relativePath));

            Assert.Contains("using MackySoft.Json.Canonicalization;", contents, StringComparison.Ordinal);
            Assert.Contains("Rfc8785JsonCanonicalizer.Canonicalize(", contents, StringComparison.Ordinal);
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Product_specific_execution_identity_paths_do_not_adopt_RFC_8785_binary64_identity ()
    {
        foreach (string relativePath in ProductSpecificExecutionIdentityPaths)
        {
            string contents = File.ReadAllText(TestRepositoryPaths.GetFullPath(relativePath));

            Assert.DoesNotContain("using MackySoft.Json.Canonicalization;", contents, StringComparison.Ordinal);
            Assert.DoesNotContain("Rfc8785JsonCanonicalizer", contents, StringComparison.Ordinal);
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Product_tree_does_not_own_RFC_8785_implementation_dependencies ()
    {
        string sourceRoot = TestRepositoryPaths.GetFullPath("src");
        var violations = new List<string>();

        foreach (string sourcePath in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
        {
            string contents = File.ReadAllText(sourcePath);
            string relativePath = GetRepositoryRelativePath(sourcePath);
            if (contents.Contains(LegacyNumberSerializerImplementationNamespace, StringComparison.Ordinal))
            {
                violations.Add(
                    $"{relativePath} directly uses the legacy RFC 8785 number serializer.");
            }
        }

        foreach (string projectPath in Directory.EnumerateFiles(sourceRoot, "*.csproj", SearchOption.AllDirectories))
        {
            string contents = File.ReadAllText(projectPath);
            if (contents.Contains(
                "PackageReference Include=\"es6numberserializer\"",
                StringComparison.Ordinal))
            {
                violations.Add(
                    $"{GetRepositoryRelativePath(projectPath)} directly references the legacy number serializer package.");
            }
        }

        Assert.False(
            File.Exists(
                TestRepositoryPaths.GetFullPath(
                    "src/MackySoft.Json.Canonicalization/MackySoft.Json.Canonicalization.csproj")),
            "The RFC 8785 provider project must remain outside the uCLI repository.");
        Assert.True(
            violations.Count == 0,
            "uCLI must consume MackySoft.Json.Canonicalization without owning its implementation dependencies: "
            + string.Join(" ", violations));
    }

    private static string GetRepositoryRelativePath (string fullPath)
    {
        return Path.GetRelativePath(TestRepositoryPaths.GetFullPath("."), fullPath)
            .Replace(Path.DirectorySeparatorChar, '/');
    }
}
