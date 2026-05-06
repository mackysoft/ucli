namespace MackySoft.Ucli.Architecture.Tests.Architecture;

public sealed class SourceBoundaryTests
{
    private static readonly string[] NonContractUcliBoundaryMarkers =
    [
        "MackySoft.Ucli.Application",
        "MackySoft.Ucli.Features",
        "MackySoft.Ucli.Hosting",
        "MackySoft.Ucli.Infrastructure",
        "MackySoft.Ucli.Shared",
        "MackySoft.Ucli.Skills",
        "MackySoft.Ucli.UnityIntegration",
        "UnityEditor",
        "UnityEngine",
    ];

    private static readonly string[] HostResourceAndCliOutputApiMarkers =
    [
        "System.Diagnostics.Process",
        "Process.Start(",
        "new Process(",
        "File.",
        "Directory.",
        "using System.IO;",
        "global using System.IO;",
        "System.IO.File",
        "System.IO.Directory",
        "System.IO.Path",
        "Path.Combine(",
        "Path.Get",
        "Path.IsPath",
        "Path.EndsInDirectorySeparator(",
        "Environment.",
        "System.Net.",
        "SocketException",
        "FileStream",
        "FileInfo",
        "DirectoryInfo",
        "HttpClient",
        "Console.",
        "System.Console",
    ];

    [Fact]
    [Trait("Size", "Small")]
    public void Contracts_source_does_not_reference_non_contract_ucli_boundaries ()
    {
        SourceBoundaryAssertions.AssertNoMarkersInCode(
            ArchitectureTestRepository.EnumerateCSharpSourceFiles("src/Ucli.Contracts"),
            NonContractUcliBoundaryMarkers);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Contracts_source_does_not_use_host_resource_or_cli_output_apis ()
    {
        SourceBoundaryAssertions.AssertNoMarkersInCode(
            ArchitectureTestRepository.EnumerateCSharpSourceFiles("src/Ucli.Contracts"),
            HostResourceAndCliOutputApiMarkers);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Contracts_ipc_validation_directory_does_not_own_csharp_source ()
    {
        var validationDirectory = ArchitectureTestRepository.ToFullPath("src/Ucli.Contracts/Ipc/Validation");
        var violations = Directory.Exists(validationDirectory)
            ? Directory
                .EnumerateFiles(validationDirectory, "*.cs", SearchOption.AllDirectories)
                .Select(ArchitectureTestRepository.NormalizeRepositoryRelativePath)
                .ToArray()
            : Array.Empty<string>();

        Assert.Empty(violations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Contracts_legacy_validation_namespace_is_limited_to_public_compatibility_types ()
    {
        var expectedPaths = new[]
        {
            "src/Ucli.Contracts/Ipc/Compatibility/JsonStringReadErrorKind.cs",
        };
        var actualPaths = ArchitectureTestRepository
            .EnumerateCSharpSourceFiles("src/Ucli.Contracts")
            .Where(static sourceFile => CSharpSourceFileReader
                .ReadWithoutCommentsAndStringLiterals(sourceFile)
                .Contains("namespace MackySoft.Ucli.Contracts.Ipc.Validation", StringComparison.Ordinal))
            .Select(ArchitectureTestRepository.NormalizeRepositoryRelativePath)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expectedPaths, actualPaths);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Contracts_contract_reading_schema_and_validation_helpers_stay_contract_pure ()
    {
        var sourceFiles = new[]
            {
                "src/Ucli.Contracts/Ipc/ContractReading",
                "src/Ucli.Contracts/Ipc/EditSteps",
                "src/Ucli.Contracts/Ipc/Operations/Contracts/Schema",
                "src/Ucli.Contracts/Ipc/Operations/Contracts/Validation",
                "src/Ucli.Contracts/Ipc/Operations/Metadata/Generation",
                "src/Ucli.Contracts/Ipc/Operations/Metadata/Validation",
            }
            .SelectMany(ArchitectureTestRepository.EnumerateCSharpSourceFiles);
        var forbiddenMarkers = NonContractUcliBoundaryMarkers
            .Concat(HostResourceAndCliOutputApiMarkers)
            .ToArray();

        SourceBoundaryAssertions.AssertNoMarkersInCode(sourceFiles, forbiddenMarkers);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Application_source_does_not_reference_host_or_adapter_namespaces ()
    {
        var forbiddenNamespaceMarkers = new[]
        {
            "MackySoft.Ucli.Hosting",
            "MackySoft.Ucli.Infrastructure",
            "MackySoft.Ucli.Shared",
            "MackySoft.Ucli.UnityIntegration",
        };

        SourceBoundaryAssertions.AssertNoMarkersInCode(
            ArchitectureTestRepository.EnumerateCSharpSourceFiles("src/Ucli.Application"),
            forbiddenNamespaceMarkers);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Application_source_does_not_use_host_resource_apis ()
    {
        string[] applicationSpecificHostResourceMarkers =
        [
            "DiagnosticsProcess",
            "IProcessRunner",
            "ProcessRunRequest",
            "ProcessRunResult",
            "ProcessRunStatus",
            "ProcessOutputDrainMode",
            "ExecuteRequestPayloadFactory",
            "ReadIndexInfoTextCodec",
            "ConsoleAppFramework",
            "Utf8JsonWriter",
            "JsonWriterOptions",
            "class UserRequestJsonNormalizer",
            "new IpcExecuteRequest",
            "IpcExecuteRequest(",
        ];
        var forbiddenSourceMarkers = HostResourceAndCliOutputApiMarkers
            .Concat(applicationSpecificHostResourceMarkers)
            .ToArray();

        SourceBoundaryAssertions.AssertNoMarkersInCode(
            ArchitectureTestRepository.EnumerateCSharpSourceFiles("src/Ucli.Application"),
            forbiddenSourceMarkers);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Infrastructure_source_does_not_reference_host_feature_or_unity_boundaries ()
    {
        var forbiddenMarkers = new[]
        {
            "ConsoleAppFramework",
            "MackySoft.Ucli.Application",
            "MackySoft.Ucli.Features",
            "MackySoft.Ucli.Hosting",
            "MackySoft.Ucli.Shared",
            "MackySoft.Ucli.Skills",
            "MackySoft.Ucli.UnityIntegration",
            "UnityEditor",
            "UnityEngine",
        };

        SourceBoundaryAssertions.AssertNoMarkersInCode(
            ArchitectureTestRepository.EnumerateCSharpSourceFiles("src/Ucli.Infrastructure"),
            forbiddenMarkers);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Application_namespace_is_declared_only_by_application_project ()
    {
        var nonApplicationSourceFiles = ArchitectureTestRepository
            .EnumerateCSharpSourceFiles("src")
            .Where(static sourceFile => !ArchitectureTestRepository
                .NormalizeRepositoryRelativePath(sourceFile)
                .StartsWith("src/Ucli.Application/", StringComparison.Ordinal));

        SourceBoundaryAssertions.AssertNoMarkersInCode(
            nonApplicationSourceFiles,
            ["namespace MackySoft.Ucli.Application"]);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Cli_host_global_usings_do_not_import_application_namespaces ()
    {
        var globalUsingsPath = ArchitectureTestRepository.ToFullPath("src/Ucli/GlobalUsings.cs");
        var sourceText = CSharpSourceFileReader.ReadWithoutCommentsAndStringLiterals(globalUsingsPath);

        Assert.DoesNotContain("global using MackySoft.Ucli.Application", sourceText, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Host_adapter_sources_do_not_import_application_use_case_namespaces ()
    {
        var hostAdapterFiles = ArchitectureTestRepository
            .EnumerateCSharpSourceFiles("src/Ucli/Features")
            .Concat(ArchitectureTestRepository.EnumerateCSharpSourceFiles("src/Ucli/UnityIntegration"))
            .Concat(ArchitectureTestRepository.EnumerateCSharpSourceFiles("src/Ucli/Shared"));

        foreach (var sourceFile in hostAdapterFiles)
        {
            var sourceText = CSharpSourceFileReader.ReadWithoutCommentsAndStringLiterals(sourceFile);
            var referencesApplicationUseCaseNamespace = ApplicationUseCaseNamespaceReferenceDetector.ContainsReference(sourceText);
            Assert.False(
                referencesApplicationUseCaseNamespace,
                $"Host adapter source must not import Application use case namespaces: {ArchitectureTestRepository.NormalizeRepositoryRelativePath(sourceFile)}");
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

        SourceBoundaryAssertions.AssertNoMarkersInCode(
            ArchitectureTestRepository.EnumerateCSharpSourceFiles("src/Ucli.Unity/Assets/MackySoft/MackySoft.Ucli.Unity"),
            forbiddenMarkers);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Unity_operation_phases_do_not_reference_contract_edit_step_lowering_helpers ()
    {
        var forbiddenMarkers = new[]
        {
            "MackySoft.Ucli.Contracts.Ipc.EditSteps",
        };

        SourceBoundaryAssertions.AssertNoMarkersInCode(
            ArchitectureTestRepository.EnumerateCSharpSourceFiles("src/Ucli.Unity/Assets/MackySoft/MackySoft.Ucli.Unity/Editor/Execution/Phases"),
            forbiddenMarkers);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Skills_project_does_not_reference_other_ucli_or_unity_boundaries ()
    {
        var forbiddenMarkers = new[]
        {
            "MackySoft.Ucli.Application",
            "MackySoft.Ucli.Contracts",
            "MackySoft.Ucli.Infrastructure",
            "UnityEngine",
        };

        SourceBoundaryAssertions.AssertNoMarkersInCode(
            ArchitectureTestRepository.EnumerateCSharpSourceFiles("src/Ucli.Skills"),
            forbiddenMarkers);
    }
}
