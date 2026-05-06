namespace MackySoft.Ucli.Architecture.Tests.Architecture;

public sealed class ApplicationOwnershipBoundaryTests
{
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
        var forbiddenCodeMarkers = new[]
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
            "IProcessRunner",
            "ProcessRunRequest",
            "ProcessRunResult",
            "ProcessRunStatus",
            "ProcessOutputDrainMode",
            "IpcPayloadCodec.SerializeToElement",
        };
        var forbiddenLiteralMarkers = new[]
        {
            "git worktree list --porcelain",
            "rev-parse",
        };
        var sourceFiles = ArchitectureTestRepository
            .EnumerateCSharpSourceFiles("src/Ucli.Application")
            .ToArray();

        SourceBoundaryAssertions.AssertNoMarkersInCode(
            sourceFiles,
            forbiddenCodeMarkers);
        Assert.Empty(SourceMarkerDetector.FindMarkersOutsideComments(sourceFiles, forbiddenLiteralMarkers));
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

        SourceBoundaryAssertions.AssertNoMarkersInCode(
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

        SourceBoundaryAssertions.AssertNoMarkersInCode(
            resultFiles,
            [
                "IpcExecute",
                "IpcError",
                "IpcResponse",
            ]);
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

        SourceBoundaryAssertions.AssertNoMarkersInCode(
            portFiles.Select(ArchitectureTestRepository.ToFullPath),
            ["IpcResponse"]);
    }
}
