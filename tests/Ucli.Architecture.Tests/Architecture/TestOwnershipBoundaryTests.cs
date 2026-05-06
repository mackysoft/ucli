namespace MackySoft.Ucli.Architecture.Tests.Architecture;

public sealed class TestOwnershipBoundaryTests
{
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

        SourceBoundaryAssertions.AssertNoMarkersInCode(
            ArchitectureTestRepository.EnumerateCSharpSourceFiles("tests/Ucli.Application.Tests"),
            forbiddenNamespaceMarkers);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Cli_host_test_global_usings_do_not_import_application_use_case_namespaces ()
    {
        var globalUsingsPath = ArchitectureTestRepository.ToFullPath("tests/Ucli.Tests/GlobalUsings.cs");
        var sourceText = CSharpSourceFileReader.ReadWithoutCommentsAndStringLiterals(globalUsingsPath);
        var referencesApplicationUseCaseNamespace = ApplicationUseCaseNamespaceReferenceDetector.ContainsReference(sourceText);

        Assert.False(
            referencesApplicationUseCaseNamespace,
            "CLI host tests must not import Application use case namespaces through global usings.");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Cli_host_infrastructure_tests_do_not_import_application_use_case_namespaces ()
    {
        var hostInfrastructureTestFiles = ArchitectureTestRepository.EnumerateCSharpSourceFiles("tests/Ucli.Tests/Features/Daemon");

        foreach (var sourceFile in hostInfrastructureTestFiles)
        {
            var sourceText = CSharpSourceFileReader.ReadWithoutCommentsAndStringLiterals(sourceFile);
            var referencesApplicationUseCaseNamespace = ApplicationUseCaseNamespaceReferenceDetector.ContainsReference(sourceText);
            Assert.False(
                referencesApplicationUseCaseNamespace,
                $"CLI host infrastructure tests must not import Application use case namespaces: {ArchitectureTestRepository.NormalizeRepositoryRelativePath(sourceFile)}");
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Application_use_case_tests_are_owned_by_application_test_project ()
    {
        var hostUseCaseTestFiles = Directory
            .EnumerateFiles(ArchitectureTestRepository.ToFullPath("tests/Ucli.Tests"), "*.cs", SearchOption.AllDirectories)
            .Select(ArchitectureTestRepository.NormalizeRepositoryRelativePath)
            .Where(static relativePath => relativePath.Contains("/UseCases/", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(hostUseCaseTestFiles);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Cli_host_tests_do_not_add_application_concrete_unit_test_classes ()
    {
        var allowedHostIntegrationTestPaths = new HashSet<string>(StringComparer.Ordinal)
        {
            "tests/Ucli.Tests/Features/Daemon/Lifecycle/Cleanup/DaemonCleanupOperationTests.cs",
            "tests/Ucli.Tests/Features/Daemon/Lifecycle/Session/DaemonSessionTokenProviderTests.cs",
            "tests/Ucli.Tests/Features/Daemon/Lifecycle/Status/DaemonStatusOperationTests.cs",
            "tests/Ucli.Tests/Features/Daemon/Observability/Logs/Daemon/LogsDaemonServiceTests.cs",
            "tests/Ucli.Tests/Features/Daemon/Observability/Logs/Unity/LogsUnityServiceTests.cs",
            "tests/Ucli.Tests/Features/Testing/Run/Results/UnityResultsConverterTests.cs",
            "tests/Ucli.Tests/ProjectContextResolverTests.cs",
        };
        var applicationConcreteTypeNames = ArchitectureTestRepository
            .EnumerateCSharpSourceFiles("src/Ucli.Application")
            .SelectMany(ConcreteTypeNameExtractor.Read)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var violations = new List<string>();
        foreach (var sourceFile in ArchitectureTestRepository.EnumerateCSharpSourceFiles("tests/Ucli.Tests"))
        {
            var relativePath = ArchitectureTestRepository.NormalizeRepositoryRelativePath(sourceFile);
            if (allowedHostIntegrationTestPaths.Contains(relativePath))
            {
                continue;
            }

            var sourceText = CSharpSourceFileReader.ReadWithoutCommentsAndStringLiterals(sourceFile);
            foreach (var typeName in applicationConcreteTypeNames)
            {
                if (sourceText.Contains($"class {typeName}Tests", StringComparison.Ordinal))
                {
                    violations.Add($"{relativePath} owns Application concrete test class {typeName}Tests.");
                }
            }
        }

        Assert.Empty(violations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Cli_host_tests_do_not_directly_instantiate_application_use_case_services ()
    {
        var applicationUseCaseServiceTypeNames = ArchitectureTestRepository
            .EnumerateCSharpSourceFiles("src/Ucli.Application/Features")
            .Where(static sourceFile => ArchitectureTestRepository
                .NormalizeRepositoryRelativePath(sourceFile)
                .Contains("/UseCases/", StringComparison.Ordinal))
            .Select(Path.GetFileNameWithoutExtension)
            .Where(static typeName => typeName is not null && typeName.EndsWith("Service", StringComparison.Ordinal))
            .Select(static typeName => typeName!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var violations = new List<string>();
        foreach (var sourceFile in ArchitectureTestRepository.EnumerateCSharpSourceFiles("tests/Ucli.Tests"))
        {
            var sourceText = CSharpSourceFileReader.ReadWithoutCommentsAndStringLiterals(sourceFile);
            foreach (var typeName in applicationUseCaseServiceTypeNames)
            {
                if (sourceText.Contains($"new {typeName}(", StringComparison.Ordinal)
                    || sourceText.Contains($"class {typeName}Tests", StringComparison.Ordinal))
                {
                    violations.Add($"{ArchitectureTestRepository.NormalizeRepositoryRelativePath(sourceFile)} references Application use case service {typeName}.");
                }
            }
        }

        Assert.Empty(violations);
    }
}
