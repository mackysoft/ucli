namespace MackySoft.Ucli.Architecture.Tests.Architecture;

public sealed class SourceBoundaryTests
{
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

        AssertNoMarkersInCode(
            ArchitectureTestRepository.EnumerateCSharpSourceFiles("src/Ucli.Contracts"),
            forbiddenMarkers);
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

        AssertNoMarkersInCode(
            ArchitectureTestRepository.EnumerateCSharpSourceFiles("src/Ucli.Application"),
            forbiddenNamespaceMarkers);
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
            "System.IO.Path",
            "Path.Combine(",
            "Path.Get",
            "Path.IsPath",
            "Path.EndsInDirectorySeparator(",
            "Environment.",
            "System.Net.Sockets",
            "System.Net.",
            "SocketException",
            "FileStream",
            "FileInfo",
            "DirectoryInfo",
            "HttpClient",
            "IProcessRunner",
            "ProcessRunRequest",
            "ProcessRunResult",
            "ProcessRunStatus",
            "ProcessOutputDrainMode",
            "ExecuteRequestPayloadFactory",
            "ReadIndexInfoTextCodec",
            "class UserRequestJsonNormalizer",
            "new IpcExecuteRequest",
            "IpcExecuteRequest(",
        };

        AssertNoMarkersInCode(
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

        AssertNoMarkersInCode(
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

        AssertNoMarkersInCode(
            nonApplicationSourceFiles,
            ["namespace MackySoft.Ucli.Application"]);
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

        AssertNoMarkersInCode(
            ArchitectureTestRepository.EnumerateCSharpSourceFiles("tests/Ucli.Application.Tests"),
            forbiddenNamespaceMarkers);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Feature_use_case_implementations_are_not_owned_by_cli_host_project ()
    {
        var hostFeatureFiles = Directory
            .EnumerateFiles(ArchitectureTestRepository.ToFullPath("src/Ucli/Features"), "*.cs", SearchOption.AllDirectories)
            .Select(ArchitectureTestRepository.NormalizeRepositoryRelativePath)
            .Where(static relativePath => relativePath.Contains("/UseCases/", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(hostFeatureFiles);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Application_project_does_not_own_host_adapter_detail_contracts ()
    {
        var forbiddenPaths = new[]
        {
            "src/Ucli.Application/Features/Daemon/Supervisor",
            "src/Ucli.Application/Shared/Execution/Process",
            "src/Ucli.Application/Shared/Git/IGitCommandClient.cs",
            "src/Ucli.Application/Shared/Git/GitCommandTextResult.cs",
            "src/Ucli.Application/Shared/Git/IGitWorktreeListPorcelainParser.cs",
            "src/Ucli.Application/Shared/Git/GitWorktreeListParseResult.cs",
        };

        foreach (var forbiddenPath in forbiddenPaths)
        {
            Assert.False(
                Directory.Exists(ArchitectureTestRepository.ToFullPath(forbiddenPath))
                || File.Exists(ArchitectureTestRepository.ToFullPath(forbiddenPath)),
                $"Application project must not own host adapter detail contract: {forbiddenPath}");
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Application_source_does_not_reintroduce_host_adapter_detail_contract_markers ()
    {
        var forbiddenSourceMarkers = new[]
        {
            "namespace MackySoft.Ucli.Application.Features.Daemon.Supervisor",
            "ResolveSelectorInputFactory",
            "QueryOptionValueNormalizer",
            "QueryAssetsFindOperationRequestFactory",
            "QueryWindowOptionsFactory",
            "StatusDaemonStateCodec",
            "DaemonStartStateCodec",
            "DaemonStatusStateCodec",
            "DaemonStopStateCodec",
            "DaemonCleanupStateCodec",
            "DaemonCleanupSkipReasonCodec",
            "DaemonListStateCodec",
            "DaemonListReasonCodec",
            "DaemonListCompletionReasonCodec",
            "ISupervisor",
            "IGitCommandClient",
            "GitCommandTextResult",
            "IGitWorktreeListPorcelainParser",
            "GitWorktreeListParseResult",
            "git worktree list --porcelain",
            "rev-parse",
            "IProcessRunner",
            "ProcessRunRequest",
            "ProcessRunResult",
            "ProcessRunStatus",
            "ProcessOutputDrainMode",
            "IpcPayloadCodec.SerializeToElement",
        };

        AssertNoRawMarkers(
            ArchitectureTestRepository.EnumerateCSharpSourceFiles("src/Ucli.Application"),
            forbiddenSourceMarkers);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Application_results_do_not_own_cli_protocol_projection_fields ()
    {
        var applicationResultFiles = new[]
        {
            "src/Ucli.Application/Features/Requests/Shared/Execution/OperationExecute/OperationExecuteResult.cs",
            "src/Ucli.Application/Features/Requests/Shared/Execution/Conversion/ExecuteResponseConversionResult.cs",
            "src/Ucli.Application/Features/Requests/Query/UseCases/Query/QueryServiceResult.cs",
            "src/Ucli.Application/Features/Requests/Resolve/UseCases/Resolve/ResolveServiceResult.cs",
        };

        AssertNoMarkersInCode(
            applicationResultFiles.Select(ArchitectureTestRepository.ToFullPath),
            ["ProtocolVersion"]);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Request_application_results_do_not_expose_ipc_dtos ()
    {
        var resultFiles = new[]
            {
                "src/Ucli.Application/Features/Requests/Shared/Execution/OperationExecute/OperationExecuteResult.cs",
                "src/Ucli.Application/Features/Requests/Query/UseCases/Query/QueryServiceResult.cs",
                "src/Ucli.Application/Features/Requests/Resolve/UseCases/Resolve/ResolveServiceResult.cs",
                "src/Ucli.Application/Features/Requests/Call/Common/Contracts/CallExecutionOutput.cs",
                "src/Ucli.Application/Features/Requests/Call/Common/Contracts/CallPlanOutput.cs",
                "src/Ucli.Application/Features/Requests/Call/Common/Contracts/CallServiceResult.cs",
                "src/Ucli.Application/Features/Requests/Plan/Common/Contracts/PlanExecutionOutput.cs",
                "src/Ucli.Application/Features/Requests/Plan/Common/Contracts/PlanServiceResult.cs",
            }
            .Select(ArchitectureTestRepository.ToFullPath)
            .Concat(ArchitectureTestRepository.EnumerateCSharpSourceFiles("src/Ucli.Application/Features/Requests/Shared/Execution/Results"));

        AssertNoMarkersInCode(
            resultFiles,
            [
                "IpcExecute",
                "IpcError",
                "IpcResponse",
            ]);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Cli_host_global_usings_do_not_import_application_namespaces ()
    {
        var globalUsingsPath = ArchitectureTestRepository.ToFullPath("src/Ucli/GlobalUsings.cs");
        var sourceText = ArchitectureTestRepository.ReadCSharpSourceWithoutCommentsAndStringLiterals(globalUsingsPath);

        Assert.DoesNotContain("global using MackySoft.Ucli.Application", sourceText, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Cli_host_test_global_usings_do_not_import_application_use_case_namespaces ()
    {
        var globalUsingsPath = ArchitectureTestRepository.ToFullPath("tests/Ucli.Tests/GlobalUsings.cs");
        var sourceText = ArchitectureTestRepository.ReadCSharpSourceWithoutCommentsAndStringLiterals(globalUsingsPath);
        var importsApplicationUseCaseNamespace = sourceText
            .Split('\n')
            .Any(IsApplicationUseCaseImport);

        Assert.False(
            importsApplicationUseCaseNamespace,
            "CLI host tests must not import Application use case namespaces through global usings.");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Cli_host_infrastructure_tests_do_not_import_application_use_case_namespaces ()
    {
        var hostInfrastructureTestFiles = ArchitectureTestRepository.EnumerateCSharpSourceFiles("tests/Ucli.Tests/Features/Daemon");

        foreach (var sourceFile in hostInfrastructureTestFiles)
        {
            var sourceText = ArchitectureTestRepository.ReadCSharpSourceWithoutCommentsAndStringLiterals(sourceFile);
            var importsApplicationUseCaseNamespace = sourceText
                .Split('\n')
                .Any(IsApplicationUseCaseImport);
            Assert.False(
                importsApplicationUseCaseNamespace,
                $"CLI host infrastructure tests must not import Application use case namespaces: {ArchitectureTestRepository.NormalizeRepositoryRelativePath(sourceFile)}");
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Unity_request_port_does_not_expose_ipc_response_envelopes ()
    {
        var portFiles = new[]
        {
            "src/Ucli.Application/Shared/Execution/UnityRequest/IUnityRequestExecutor.cs",
            "src/Ucli.Application/Shared/Execution/UnityRequest/UnityRequestExecutionResult.cs",
            "src/Ucli.Application/Shared/Execution/UnityRequest/UnityRequestResponse.cs",
        };

        AssertNoMarkersInCode(
            portFiles.Select(ArchitectureTestRepository.ToFullPath),
            ["IpcResponse"]);
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
            .SelectMany(ArchitectureTestRepository.ReadConcreteTypeNames)
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

            var sourceText = ArchitectureTestRepository.ReadCSharpSourceWithoutCommentsAndStringLiterals(sourceFile);
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
            var sourceText = ArchitectureTestRepository.ReadCSharpSourceWithoutCommentsAndStringLiterals(sourceFile);
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
            var sourceText = ArchitectureTestRepository.ReadCSharpSourceWithoutCommentsAndStringLiterals(sourceFile);
            var importsApplicationUseCaseNamespace = sourceText
                .Split('\n')
                .Any(IsApplicationUseCaseImport);
            Assert.False(
                importsApplicationUseCaseNamespace,
                $"Host adapter source must not import Application use case namespaces: {ArchitectureTestRepository.NormalizeRepositoryRelativePath(sourceFile)}");
        }
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
            ["src/Ucli.Infrastructure/AssemblyInfo.cs"] =
            [
                "MackySoft.Ucli",
                "MackySoft.Ucli.Infrastructure.Tests",
                "MackySoft.Ucli.Tests",
                "MackySoft.Ucli.Unity.Editor",
                "MackySoft.Ucli.Unity.Tests.Editor",
            ],
            ["src/Ucli/Hosting/AssemblyInfo.cs"] =
            [
                "MackySoft.Ucli.Tests",
            ],
            ["src/Ucli.Unity/Assets/MackySoft/MackySoft.Ucli.Unity/Editor/AssemblyInfo.cs"] =
            [
                "MackySoft.Ucli.Unity.Tests.Editor",
            ],
            ["tests/Tests.Helper/AssemblyInfo.cs"] =
            [
                "MackySoft.Ucli.Application.Tests",
                "MackySoft.Ucli.Contracts.Tests",
                "MackySoft.Ucli.Infrastructure.Tests",
                "MackySoft.Ucli.Skills.Tests",
                "MackySoft.Ucli.Tests",
            ],
        };

        var actualAssemblyInfoFiles = ArchitectureTestRepository
            .EnumerateRepositoryAssemblyInfoFiles()
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            expectedFriendsByAssemblyInfo.Keys.OrderBy(static value => value, StringComparer.Ordinal),
            actualAssemblyInfoFiles);

        foreach (var (assemblyInfoPath, expectedFriends) in expectedFriendsByAssemblyInfo)
        {
            var actualFriends = ArchitectureTestRepository.ReadInternalsVisibleToAssemblyNames(assemblyInfoPath);
            Assert.Equal(
                expectedFriends.OrderBy(static value => value, StringComparer.Ordinal),
                actualFriends.OrderBy(static value => value, StringComparer.Ordinal));
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

        AssertNoMarkersInCode(
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

        AssertNoMarkersInCode(
            ArchitectureTestRepository.EnumerateCSharpSourceFiles("src/Ucli.Skills"),
            forbiddenMarkers);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Public_surface_does_not_expose_internal_implementation_namespaces ()
    {
        var forbiddenPublicSurfaceMarkers = new[]
        {
            "MackySoft.Ucli.Application.Features",
            "MackySoft.Ucli.Application.Shared",
            "MackySoft.Ucli.Features",
            "MackySoft.Ucli.Hosting",
            "MackySoft.Ucli.Shared",
            "MackySoft.Ucli.UnityIntegration",
        };
        var sourceFiles = new[]
            {
                "src/Ucli",
                "src/Ucli.Application",
                "src/Ucli.Contracts",
                "src/Ucli.Infrastructure",
                "src/Ucli.Skills",
            }
            .SelectMany(ArchitectureTestRepository.EnumerateCSharpSourceFiles);

        var violations = new List<string>();
        foreach (var sourceFile in sourceFiles)
        {
            foreach (var declaration in ArchitectureTestRepository.ReadPublicSurfaceDeclarations(sourceFile))
            {
                foreach (var marker in forbiddenPublicSurfaceMarkers)
                {
                    if (declaration.Namespace.StartsWith(marker, StringComparison.Ordinal)
                        || declaration.Signature.Contains(marker, StringComparison.Ordinal))
                    {
                        violations.Add($"{declaration.RelativePath}:{declaration.LineNumber} exposes {marker}.");
                    }
                }
            }
        }

        Assert.Empty(violations);
    }

    private static void AssertNoMarkersInCode (IEnumerable<string> sourceFiles, IReadOnlyCollection<string> forbiddenMarkers)
    {
        var violations = new List<string>();
        foreach (var sourceFile in sourceFiles)
        {
            var sourceText = ArchitectureTestRepository.ReadCSharpSourceWithoutCommentsAndStringLiterals(sourceFile);
            AddMarkerViolations(violations, sourceFile, sourceText, forbiddenMarkers);
        }

        Assert.Empty(violations);
    }

    private static void AssertNoRawMarkers (IEnumerable<string> sourceFiles, IReadOnlyCollection<string> forbiddenMarkers)
    {
        var violations = new List<string>();
        foreach (var sourceFile in sourceFiles)
        {
            var sourceText = File.ReadAllText(sourceFile);
            AddMarkerViolations(violations, sourceFile, sourceText, forbiddenMarkers);
        }

        Assert.Empty(violations);
    }

    private static void AddMarkerViolations (
        List<string> violations,
        string sourceFile,
        string sourceText,
        IReadOnlyCollection<string> forbiddenMarkers)
    {
        foreach (var marker in forbiddenMarkers)
        {
            if (sourceText.Contains(marker, StringComparison.Ordinal))
            {
                violations.Add($"{ArchitectureTestRepository.NormalizeRepositoryRelativePath(sourceFile)} contains {marker}.");
            }
        }
    }

    private static bool IsApplicationUseCaseImport (string line)
    {
        var trimmedLine = line.TrimStart();
        return (trimmedLine.StartsWith("using ", StringComparison.Ordinal)
            || trimmedLine.StartsWith("global using ", StringComparison.Ordinal))
            && trimmedLine.Contains("MackySoft.Ucli.Application.Features.", StringComparison.Ordinal)
            && trimmedLine.Contains(".UseCases.", StringComparison.Ordinal);
    }
}
