using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace MackySoft.Ucli.Architecture.Tests.Architecture;

public sealed class ProjectBoundaryTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    [Trait("Size", "Small")]
    public void ProductionProjects_reference_only_allowed_projects ()
    {
        var expectedReferencesByProject = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["src/Ucli.Application/Ucli.Application.csproj"] =
            [
                "src/Ucli.Contracts/Ucli.Contracts.csproj",
            ],
            ["src/Ucli.Contracts/Ucli.Contracts.csproj"] = [],
            ["src/Ucli.Infrastructure/Ucli.Infrastructure.csproj"] =
            [
                "src/Ucli.Contracts/Ucli.Contracts.csproj",
            ],
            ["src/Ucli.Skills/Ucli.Skills.csproj"] = [],
            ["src/Ucli/Ucli.csproj"] =
            [
                "src/Ucli.Application/Ucli.Application.csproj",
                "src/Ucli.Contracts/Ucli.Contracts.csproj",
                "src/Ucli.Infrastructure/Ucli.Infrastructure.csproj",
                "src/Ucli.Skills/Ucli.Skills.csproj",
            ],
        };

        var actualProjectPaths = EnumerateProductionProjectFiles()
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            expectedReferencesByProject.Keys.OrderBy(static value => value, StringComparer.Ordinal),
            actualProjectPaths);

        foreach (var (projectPath, expectedReferences) in expectedReferencesByProject)
        {
            var actualReferences = ReadProjectReferences(projectPath);
            Assert.Equal(
                expectedReferences.OrderBy(static value => value, StringComparer.Ordinal),
                actualReferences.OrderBy(static value => value, StringComparer.Ordinal));
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TestProjects_reference_only_allowed_projects ()
    {
        var expectedReferencesByProject = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["tests/Ucli.Architecture.Tests/Ucli.Architecture.Tests.csproj"] = [],
            ["tests/Tests.Helper/Tests.Helper.csproj"] = [],
            ["tests/Ucli.Application.Tests/Ucli.Application.Tests.csproj"] =
            [
                "src/Ucli.Application/Ucli.Application.csproj",
                "tests/Tests.Helper/Tests.Helper.csproj",
            ],
            ["tests/Ucli.Contracts.Tests/Ucli.Contracts.Tests.csproj"] =
            [
                "src/Ucli.Contracts/Ucli.Contracts.csproj",
                "tests/Tests.Helper/Tests.Helper.csproj",
            ],
            ["tests/Ucli.Infrastructure.Tests/Ucli.Infrastructure.Tests.csproj"] =
            [
                "src/Ucli.Contracts/Ucli.Contracts.csproj",
                "src/Ucli.Infrastructure/Ucli.Infrastructure.csproj",
                "tests/Tests.Helper/Tests.Helper.csproj",
            ],
            ["tests/Ucli.Skills.Tests/Ucli.Skills.Tests.csproj"] =
            [
                "src/Ucli.Skills/Ucli.Skills.csproj",
                "tests/Tests.Helper/Tests.Helper.csproj",
            ],
            ["tests/Ucli.Tests/Ucli.Tests.csproj"] =
            [
                "src/Ucli.Application/Ucli.Application.csproj",
                "src/Ucli.Contracts/Ucli.Contracts.csproj",
                "src/Ucli.Infrastructure/Ucli.Infrastructure.csproj",
                "src/Ucli/Ucli.csproj",
                "tests/Tests.Helper/Tests.Helper.csproj",
            ],
        };

        var actualProjectPaths = Directory
            .EnumerateFiles(Path.Combine(RepositoryRoot, "tests"), "*.csproj", SearchOption.AllDirectories)
            .Select(NormalizeRepositoryRelativePath)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            expectedReferencesByProject.Keys.OrderBy(static value => value, StringComparer.Ordinal),
            actualProjectPaths);

        foreach (var (projectPath, expectedReferences) in expectedReferencesByProject)
        {
            var actualReferences = ReadProjectReferences(projectPath);
            Assert.Equal(
                expectedReferences.OrderBy(static value => value, StringComparer.Ordinal),
                actualReferences.OrderBy(static value => value, StringComparer.Ordinal));
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ProductionProjects_use_only_allowed_packages ()
    {
        var expectedPackagesByProject = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["src/Ucli.Application/Ucli.Application.csproj"] =
            [
                "Microsoft.Extensions.DependencyInjection.Abstractions",
            ],
            ["src/Ucli.Contracts/Ucli.Contracts.csproj"] =
            [
                "System.Text.Json",
            ],
            ["src/Ucli.Infrastructure/Ucli.Infrastructure.csproj"] = [],
            ["src/Ucli.Skills/Ucli.Skills.csproj"] = [],
            ["src/Ucli/Ucli.csproj"] =
            [
                "ConsoleAppFramework",
                "Microsoft.Extensions.DependencyInjection",
            ],
        };

        foreach (var (projectPath, expectedPackages) in expectedPackagesByProject)
        {
            var actualPackages = ReadPackageReferences(projectPath);
            Assert.Equal(
                expectedPackages.OrderBy(static value => value, StringComparer.Ordinal),
                actualPackages.OrderBy(static value => value, StringComparer.Ordinal));
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TestProjects_use_only_allowed_packages ()
    {
        string[] expectedTestPackages =
        [
            "coverlet.collector",
            "Microsoft.NET.Test.Sdk",
            "xunit",
            "xunit.runner.visualstudio",
        ];
        var expectedPackagesByProject = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["tests/Ucli.Architecture.Tests/Ucli.Architecture.Tests.csproj"] = expectedTestPackages,
            ["tests/Tests.Helper/Tests.Helper.csproj"] = expectedTestPackages,
            ["tests/Ucli.Application.Tests/Ucli.Application.Tests.csproj"] = expectedTestPackages,
            ["tests/Ucli.Contracts.Tests/Ucli.Contracts.Tests.csproj"] = expectedTestPackages,
            ["tests/Ucli.Infrastructure.Tests/Ucli.Infrastructure.Tests.csproj"] = expectedTestPackages,
            ["tests/Ucli.Skills.Tests/Ucli.Skills.Tests.csproj"] = expectedTestPackages,
            ["tests/Ucli.Tests/Ucli.Tests.csproj"] = expectedTestPackages,
        };

        foreach (var (projectPath, expectedPackages) in expectedPackagesByProject)
        {
            var actualPackages = ReadPackageReferences(projectPath);
            Assert.Equal(
                expectedPackages.OrderBy(static value => value, StringComparer.Ordinal),
                actualPackages.OrderBy(static value => value, StringComparer.Ordinal));
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Verify_scope_detector_tracks_application_project_changes ()
    {
        var sourceText = File.ReadAllText(Path.Combine(RepositoryRoot, "scripts", "detect-verify-scopes.sh"));
        var applicationScopeOccurrences = sourceText.Split("src/Ucli.Application/*", StringSplitOptions.None).Length - 1;

        Assert.True(
            applicationScopeOccurrences >= 2,
            "Application project changes must trigger both .NET verification and CLI package verification scopes.");
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

        var applicationSourceFiles = EnumerateCSharpSourceFiles("src/Ucli.Application");

        foreach (var sourceFile in applicationSourceFiles)
        {
            var sourceText = File.ReadAllText(sourceFile);
            foreach (var marker in forbiddenNamespaceMarkers)
            {
                Assert.DoesNotContain(marker, sourceText, StringComparison.Ordinal);
            }
        }
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

        var applicationSourceFiles = EnumerateCSharpSourceFiles("src/Ucli.Application");

        foreach (var sourceFile in applicationSourceFiles)
        {
            var sourceText = File.ReadAllText(sourceFile);
            foreach (var marker in forbiddenSourceMarkers)
            {
                Assert.DoesNotContain(marker, sourceText, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Application_namespace_is_declared_only_by_application_project ()
    {
        var nonApplicationSourceFiles = EnumerateCSharpSourceFiles("src")
            .Where(sourceFile => !NormalizeRepositoryRelativePath(sourceFile).StartsWith("src/Ucli.Application/", StringComparison.Ordinal));

        foreach (var sourceFile in nonApplicationSourceFiles)
        {
            var sourceText = File.ReadAllText(sourceFile);
            Assert.DoesNotContain("namespace MackySoft.Ucli.Application", sourceText, StringComparison.Ordinal);
        }
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

        var applicationTestFiles = EnumerateCSharpSourceFiles("tests/Ucli.Application.Tests")
            .Where(static sourceFile => !sourceFile.EndsWith("ProjectBoundaryTests.cs", StringComparison.Ordinal));

        foreach (var sourceFile in applicationTestFiles)
        {
            var sourceText = File.ReadAllText(sourceFile);
            foreach (var marker in forbiddenNamespaceMarkers)
            {
                Assert.DoesNotContain(marker, sourceText, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Feature_use_case_implementations_are_not_owned_by_cli_host_project ()
    {
        var hostFeatureFiles = Directory
            .EnumerateFiles(Path.Combine(RepositoryRoot, "src", "Ucli", "Features"), "*.cs", SearchOption.AllDirectories)
            .Select(NormalizeRepositoryRelativePath)
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
                Directory.Exists(Path.Combine(RepositoryRoot, forbiddenPath))
                || File.Exists(Path.Combine(RepositoryRoot, forbiddenPath)),
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

        var applicationSourceFiles = EnumerateCSharpSourceFiles("src/Ucli.Application");

        foreach (var sourceFile in applicationSourceFiles)
        {
            var sourceText = File.ReadAllText(sourceFile);
            foreach (var marker in forbiddenSourceMarkers)
            {
                Assert.DoesNotContain(marker, sourceText, StringComparison.Ordinal);
            }
        }
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

        foreach (var resultFile in applicationResultFiles)
        {
            var sourceText = File.ReadAllText(Path.Combine(RepositoryRoot, resultFile));
            Assert.DoesNotContain("ProtocolVersion", sourceText, StringComparison.Ordinal);
        }
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
            .Select(static path => Path.Combine(RepositoryRoot, path))
            .Concat(EnumerateCSharpSourceFiles("src/Ucli.Application/Features/Requests/Shared/Execution/Results"));

        foreach (var resultFile in resultFiles)
        {
            var sourceText = File.ReadAllText(resultFile);

            Assert.DoesNotContain("IpcExecute", sourceText, StringComparison.Ordinal);
            Assert.DoesNotContain("IpcError", sourceText, StringComparison.Ordinal);
            Assert.DoesNotContain("IpcResponse", sourceText, StringComparison.Ordinal);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Cli_host_global_usings_do_not_import_application_namespaces ()
    {
        var globalUsingsPath = Path.Combine(RepositoryRoot, "src", "Ucli", "GlobalUsings.cs");
        var sourceText = File.ReadAllText(globalUsingsPath);

        Assert.DoesNotContain("global using MackySoft.Ucli.Application", sourceText, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Cli_host_test_global_usings_do_not_import_application_use_case_namespaces ()
    {
        var globalUsingsPath = Path.Combine(RepositoryRoot, "tests", "Ucli.Tests", "GlobalUsings.cs");
        var sourceText = File.ReadAllText(globalUsingsPath);
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
        var hostInfrastructureTestFiles = EnumerateCSharpSourceFiles("tests/Ucli.Tests/Features/Daemon");

        foreach (var sourceFile in hostInfrastructureTestFiles)
        {
            var sourceText = File.ReadAllText(sourceFile);
            var importsApplicationUseCaseNamespace = sourceText
                .Split('\n')
                .Any(IsApplicationUseCaseImport);
            Assert.False(
                importsApplicationUseCaseNamespace,
                $"CLI host infrastructure tests must not import Application use case namespaces: {NormalizeRepositoryRelativePath(sourceFile)}");
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

        foreach (var portFile in portFiles)
        {
            var sourceText = File.ReadAllText(Path.Combine(RepositoryRoot, portFile));
            Assert.DoesNotContain("IpcResponse", sourceText, StringComparison.Ordinal);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Application_use_case_tests_are_owned_by_application_test_project ()
    {
        var hostUseCaseTestFiles = Directory
            .EnumerateFiles(Path.Combine(RepositoryRoot, "tests", "Ucli.Tests"), "*.cs", SearchOption.AllDirectories)
            .Select(NormalizeRepositoryRelativePath)
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
        var applicationConcreteTypeNames = EnumerateCSharpSourceFiles("src/Ucli.Application")
            .SelectMany(ReadConcreteTypeNames)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var violations = new List<string>();
        foreach (var sourceFile in EnumerateCSharpSourceFiles("tests/Ucli.Tests"))
        {
            var relativePath = NormalizeRepositoryRelativePath(sourceFile);
            if (allowedHostIntegrationTestPaths.Contains(relativePath))
            {
                continue;
            }

            var sourceText = File.ReadAllText(sourceFile);
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
        var applicationUseCaseServiceTypeNames = EnumerateCSharpSourceFiles("src/Ucli.Application/Features")
            .Where(static sourceFile => NormalizeRepositoryRelativePath(sourceFile).Contains("/UseCases/", StringComparison.Ordinal))
            .Select(Path.GetFileNameWithoutExtension)
            .Where(static typeName => typeName is not null && typeName.EndsWith("Service", StringComparison.Ordinal))
            .Select(static typeName => typeName!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var violations = new List<string>();
        foreach (var sourceFile in EnumerateCSharpSourceFiles("tests/Ucli.Tests"))
        {
            var sourceText = File.ReadAllText(sourceFile);
            foreach (var typeName in applicationUseCaseServiceTypeNames)
            {
                if (sourceText.Contains($"new {typeName}(", StringComparison.Ordinal)
                    || sourceText.Contains($"class {typeName}Tests", StringComparison.Ordinal))
                {
                    violations.Add($"{NormalizeRepositoryRelativePath(sourceFile)} references Application use case service {typeName}.");
                }
            }
        }

        Assert.Empty(violations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Host_adapter_sources_do_not_import_application_use_case_namespaces ()
    {
        var hostAdapterFiles = EnumerateCSharpSourceFiles("src/Ucli/Features")
            .Concat(EnumerateCSharpSourceFiles("src/Ucli/UnityIntegration"))
            .Concat(EnumerateCSharpSourceFiles("src/Ucli/Shared"));

        foreach (var sourceFile in hostAdapterFiles)
        {
            var sourceText = File.ReadAllText(sourceFile);
            var importsApplicationUseCaseNamespace = sourceText
                .Split('\n')
                .Any(IsApplicationUseCaseImport);
            Assert.False(
                importsApplicationUseCaseNamespace,
                $"Host adapter source must not import Application use case namespaces: {NormalizeRepositoryRelativePath(sourceFile)}");
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

        var actualAssemblyInfoFiles = EnumerateRepositoryAssemblyInfoFiles()
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            expectedFriendsByAssemblyInfo.Keys.OrderBy(static value => value, StringComparer.Ordinal),
            actualAssemblyInfoFiles);

        foreach (var (assemblyInfoPath, expectedFriends) in expectedFriendsByAssemblyInfo)
        {
            var actualFriends = ReadInternalsVisibleToAssemblyNames(assemblyInfoPath);
            Assert.Equal(
                expectedFriends.OrderBy(static value => value, StringComparer.Ordinal),
                actualFriends.OrderBy(static value => value, StringComparer.Ordinal));
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Unity_asmdefs_do_not_reference_cli_host_or_application_assemblies ()
    {
        var forbiddenAssemblyReferences = new[]
        {
            "\"MackySoft.Ucli\"",
            "\"MackySoft.Ucli.Application\"",
        };

        var asmdefFiles = Directory.EnumerateFiles(
            Path.Combine(RepositoryRoot, "src", "Ucli.Unity", "Assets", "MackySoft", "MackySoft.Ucli.Unity"),
            "*.asmdef",
            SearchOption.AllDirectories);

        foreach (var asmdefFile in asmdefFiles)
        {
            var asmdefText = File.ReadAllText(asmdefFile);
            foreach (var marker in forbiddenAssemblyReferences)
            {
                Assert.DoesNotContain(marker, asmdefText, StringComparison.Ordinal);
            }
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

        var unitySourceFiles = EnumerateCSharpSourceFiles("src/Ucli.Unity/Assets/MackySoft/MackySoft.Ucli.Unity");

        foreach (var sourceFile in unitySourceFiles)
        {
            var sourceText = File.ReadAllText(sourceFile);
            foreach (var marker in forbiddenMarkers)
            {
                Assert.DoesNotContain(marker, sourceText, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Skills_project_does_not_reference_application_or_unity_boundaries ()
    {
        var forbiddenMarkers = new[]
        {
            "MackySoft.Ucli.Application",
            "MackySoft.Ucli.Infrastructure",
            "UnityEngine",
        };

        var skillSourceFiles = EnumerateCSharpSourceFiles("src/Ucli.Skills");

        foreach (var sourceFile in skillSourceFiles)
        {
            var sourceText = File.ReadAllText(sourceFile);
            foreach (var marker in forbiddenMarkers)
            {
                Assert.DoesNotContain(marker, sourceText, StringComparison.Ordinal);
            }
        }
    }

    private static IEnumerable<string> EnumerateCSharpSourceFiles (string repositoryRelativeDirectory)
    {
        return Directory
            .EnumerateFiles(Path.Combine(RepositoryRoot, repositoryRelativeDirectory), "*.cs", SearchOption.AllDirectories)
            .Where(static sourceFile =>
            {
                var relativePath = NormalizeRepositoryRelativePath(sourceFile);
                return !relativePath.Contains("/bin/", StringComparison.Ordinal)
                    && !relativePath.Contains("/obj/", StringComparison.Ordinal)
                    && !relativePath.Contains("/Library/", StringComparison.Ordinal)
                    && !relativePath.Contains("/Temp/", StringComparison.Ordinal);
            });
    }

    private static IEnumerable<string> EnumerateProductionProjectFiles ()
    {
        return Directory
            .EnumerateFiles(Path.Combine(RepositoryRoot, "src"), "*.csproj", SearchOption.AllDirectories)
            .Select(NormalizeRepositoryRelativePath)
            .Where(static relativePath => !IsUnityGeneratedProjectFile(relativePath));
    }

    private static IEnumerable<string> EnumerateRepositoryAssemblyInfoFiles ()
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
            .Select(static relativeRoot => Path.Combine(RepositoryRoot, relativeRoot))
            .SelectMany(static root => Directory.EnumerateFiles(root, "AssemblyInfo.cs", SearchOption.AllDirectories))
            .Select(NormalizeRepositoryRelativePath)
            .Where(static relativePath => !relativePath.Contains("/bin/", StringComparison.Ordinal)
                                          && !relativePath.Contains("/obj/", StringComparison.Ordinal));
    }

    private static bool IsUnityGeneratedProjectFile (string relativePath)
    {
        return relativePath.StartsWith("src/Ucli.Unity/", StringComparison.Ordinal)
            && relativePath.EndsWith(".csproj", StringComparison.Ordinal);
    }

    private static bool IsApplicationUseCaseImport (string line)
    {
        var trimmedLine = line.TrimStart();
        return (trimmedLine.StartsWith("using ", StringComparison.Ordinal)
            || trimmedLine.StartsWith("global using ", StringComparison.Ordinal))
            && trimmedLine.Contains("MackySoft.Ucli.Application.Features.", StringComparison.Ordinal)
            && trimmedLine.Contains(".UseCases.", StringComparison.Ordinal);
    }

    private static string[] ReadProjectReferences (string projectPath)
    {
        var projectFullPath = Path.Combine(RepositoryRoot, projectPath);
        var projectDirectory = Path.GetDirectoryName(projectFullPath)
            ?? throw new InvalidOperationException($"Project path has no directory: {projectFullPath}");
        var document = XDocument.Load(projectFullPath);
        return document
            .Descendants("ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(value => NormalizeRepositoryRelativePath(Path.GetFullPath(Path.Combine(projectDirectory, value!))))
            .ToArray();
    }

    private static string[] ReadPackageReferences (string projectPath)
    {
        var projectFullPath = Path.Combine(RepositoryRoot, projectPath);
        var document = XDocument.Load(projectFullPath);
        return document
            .Descendants("PackageReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value!)
            .ToArray();
    }

    private static IEnumerable<string> ReadConcreteTypeNames (string sourceFile)
    {
        var sourceText = File.ReadAllText(sourceFile);
        return Regex
            .Matches(
                sourceText,
                @"\b(?:class|struct)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\b|\brecord\s+(?:class\s+|struct\s+)?(?<name>[A-Za-z_][A-Za-z0-9_]*)\b")
            .Select(static match => match.Groups["name"].Value)
            .Where(static value => !string.IsNullOrWhiteSpace(value));
    }

    private static string[] ReadInternalsVisibleToAssemblyNames (string assemblyInfoPath)
    {
        var sourceText = File.ReadAllText(Path.Combine(RepositoryRoot, assemblyInfoPath));
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

    private static string NormalizeRepositoryRelativePath (string fullPath)
    {
        return Path.GetRelativePath(RepositoryRoot, fullPath).Replace('\\', '/');
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
