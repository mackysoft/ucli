using System.Text.Json;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.OperationCatalog.Catalog.Source;
using MackySoft.Ucli.Shared.Configuration;
using MackySoft.Ucli.Shared.Context.Project;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Shared.Execution.UnityRequest;

namespace MackySoft.Ucli.Tests.Ops.Source;

public sealed class OpsCatalogReaderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenResponseIsSuccessful_ReturnsCatalogPayload ()
    {
        var executor = new StubUnityRequestExecutor
        {
            Result = UnityRequestExecutionResult.Success(CreateResponse(
                IpcProtocol.StatusOk,
                Array.Empty<IpcError>(),
                new IpcOpsReadResponse(
                    DateTimeOffset.Parse("2026-03-07T00:00:00+00:00"),
                    [
                        new IndexOpEntryJsonContract(
                            Name: UcliPrimitiveOperationNames.GoDescribe,
                            Kind: "query",
                            Policy: "safe",
                            ArgsSchemaJson: """{"type":"object"}"""),
                    ]))),
        };
        var reader = new OpsCatalogReader(executor);

        var result = await reader.Read(
            CreateProjectContext(),
            UcliConfig.CreateDefault(),
            UnityExecutionMode.Daemon,
            TimeSpan.FromMilliseconds(1200),
            failFast: true,
            requireReadinessGate: false,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Response);
        Assert.Single(result.Response.Operations!);
        Assert.Equal(UcliPrimitiveOperationNames.GoDescribe, result.Response.Operations![0].Name);
        Assert.Equal(UcliCommandIds.Ops, executor.Command.Name);
        Assert.Equal(IpcMethodNames.OpsRead, executor.Method);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenResponseContainsIpcFailure_ReturnsFailure ()
    {
        var executor = new StubUnityRequestExecutor
        {
            Result = UnityRequestExecutionResult.Success(CreateResponse(
                IpcProtocol.StatusError,
                [
                    new IpcError(
                        IpcErrorCodes.InvalidArgument,
                        "invalid request",
                        null),
                ],
                new { })),
        };
        var reader = new OpsCatalogReader(executor);

        var result = await reader.Read(
            CreateProjectContext(),
            UcliConfig.CreateDefault(),
            UnityExecutionMode.Auto,
            TimeSpan.FromMilliseconds(1200),
            failFast: false,
            requireReadinessGate: true,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(IpcErrorCodes.InvalidArgument, result.ErrorCode);
        Assert.Equal("invalid request", result.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenPayloadIsMalformed_ReturnsFailure ()
    {
        var executor = new StubUnityRequestExecutor
        {
            Result = UnityRequestExecutionResult.Success(CreateResponse(
                IpcProtocol.StatusOk,
                Array.Empty<IpcError>(),
                new
                {
                    generatedAtUtc = "2026-03-07T00:00:00+00:00",
                })),
        };
        var reader = new OpsCatalogReader(executor);

        var result = await reader.Read(
            CreateProjectContext(),
            UcliConfig.CreateDefault(),
            UnityExecutionMode.Auto,
            TimeSpan.FromMilliseconds(1200),
            failFast: false,
            requireReadinessGate: false,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(IpcErrorCodes.InternalError, result.ErrorCode);
        Assert.Contains("payload is invalid", result.Message, StringComparison.Ordinal);
    }

    private static ResolvedUnityProjectContext CreateProjectContext ()
    {
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: "/repo/UnityProject",
            RepositoryRoot: "/repo",
            ProjectFingerprint: "project-fingerprint",
            PathSource: UnityProjectPathSource.CommandOption);
    }

    private static IpcResponse CreateResponse (
        string status,
        IReadOnlyList<IpcError> errors,
        object payload)
    {
        return new IpcResponse(
            IpcProtocol.CurrentVersion,
            "req-ops-1",
            status,
            IpcPayloadCodec.SerializeToElement(payload),
            errors);
    }

    private sealed class StubUnityRequestExecutor : IUnityRequestExecutor
    {
        public UnityRequestExecutionResult Result { get; set; } = null!;

        public UcliCommand Command { get; private set; } = new("pending");

        public string Method { get; private set; } = string.Empty;

        public ValueTask<UnityRequestExecutionResult> Execute (
            UcliCommand command,
            UnityExecutionMode mode,
            TimeSpan timeout,
            UcliConfig config,
            ResolvedUnityProjectContext unityProject,
            string method,
            JsonElement payload,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Command = command;
            Method = method;
            return ValueTask.FromResult(Result);
        }
    }
}
