namespace MackySoft.Ucli.Architecture.Tests.Architecture;

public sealed class SourceBoundaryTests
{
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
        var forbiddenMarkers = new[]
        {
            "MackySoft.Ucli.Application",
            "MackySoft.Ucli.Features",
            "MackySoft.Ucli.Hosting",
            "MackySoft.Ucli.Infrastructure",
            "MackySoft.Ucli.Shared",
            "MackySoft.Ucli.Skills",
            "MackySoft.Ucli.UnityIntegration",
        };

        SourceBoundaryAssertions.AssertNoMarkersInCode(
            ArchitectureTestRepository.EnumerateCSharpSourceFiles("src/Ucli.Contracts"),
            forbiddenMarkers);
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
