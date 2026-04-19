using System.Text.Json;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Requests.Plan;
using MackySoft.Ucli.Features.Requests.Shared.Execution;
using MackySoft.Ucli.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Features.Requests.Validate;
using MackySoft.Ucli.Hosting.Cli;
using MackySoft.Ucli.Hosting.Cli.Requests;
using MackySoft.Ucli.Shared.Configuration;
using MackySoft.Ucli.Shared.Context;
using MackySoft.Ucli.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Shared.Execution.Process;
using MackySoft.Ucli.Shared.Execution.Timeout;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Shared.Foundation;
using MackySoft.Ucli.UnityIntegration.Indexing.ReadIndex;
using MackySoft.Ucli.UnityIntegration.Ipc;
using MackySoft.Ucli.UnityIntegration.Project;

namespace MackySoft.Ucli.Tests;

public sealed class PlanServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenReadIndexModeIsDisabled_UsesSyntaxOnlyValidationAndPlanIpc ()
    {
        var validator = new SpyRequestStaticValidator(ValidationResult.Success());
        var metadataResolver = new StubValidateMetadataResolver(CreateMetadataSuccessResult());
        var unityIpcRequestExecutor = new SpyUnityIpcRequestExecutor(UnityIpcRequestExecutionResult.Success(
            CreateResponse(
                status: IpcProtocol.StatusOk,
                opResults:
                [
                    new IpcExecuteOperationResult(
                        OpId: "step-1",
                        Op: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe,
                        Phase: IpcExecuteOperationPhaseNames.Plan,
                        Applied: false,
                        Changed: false,
                        Touched: []),
                ],
                errors: [],
                planToken: "plan-token-1")));
        var service = new PlanService(
            new StubRequestPreparationService(ParsedRequestResult.Success(CreateParsedRequestContext())),
            new StubProjectContextResolver(ProjectContextResolutionResult.Success(CreateProjectContext())),
            metadataResolver,
            validator,
            unityIpcRequestExecutor);

        var result = await service.Execute(
            new PlanCommandInput(
                RequestPath: null,
                ProjectPath: "/repo/UnityProject",
                Mode: "oneshot",
                Timeout: "1234",
                ReadIndexMode: ReadIndexModeValues.Disabled,
                FailFast: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("uCLI plan completed.", result.Message);
        Assert.NotNull(result.Output);
        Assert.Equal("9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62", result.Output!.RequestId);
        Assert.Equal("plan-token-1", result.Output.PlanToken);
        Assert.False(result.Output.ReadIndex.Used);
        Assert.False(result.Output.ReadIndex.Hit);
        Assert.Equal("readIndex disabled by mode.", result.Output.ReadIndex.FallbackReason);
        Assert.False(validator.LastCatalog!.IsAvailable);
        Assert.Equal(0, metadataResolver.CallCount);
        Assert.Equal(1, unityIpcRequestExecutor.CallCount);
        Assert.Equal(UcliCommandIds.Plan, unityIpcRequestExecutor.CapturedCommand);
        Assert.Equal(UnityExecutionMode.Oneshot, unityIpcRequestExecutor.CapturedMode);
        Assert.Equal(TimeSpan.FromMilliseconds(1234), unityIpcRequestExecutor.CapturedTimeout);
        Assert.Equal(IpcMethodNames.Execute, unityIpcRequestExecutor.CapturedMethod);

        Assert.True(IpcPayloadCodec.TryDeserialize(
            unityIpcRequestExecutor.CapturedPayload,
            out IpcExecuteRequest? executeRequest,
            out var payloadError));
        Assert.Equal(IpcPayloadReadError.None, payloadError);
        Assert.NotNull(executeRequest);
        Assert.Equal(UcliCommandIds.Plan, executeRequest!.Command);
        Assert.True(executeRequest.FailFast);
        Assert.Null(executeRequest.PlanToken);
        Assert.Equal("9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62", executeRequest.Arguments.GetProperty("requestId").GetString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenAllowStaleFallsBackToSyntaxOnly_ContinuesToUnityExecution ()
    {
        var validator = new SpyRequestStaticValidator(ValidationResult.Success());
        var readIndex = CreateReadIndexInfo(
            used: false,
            hit: false,
            freshness: ReadIndexInfoTextCodec.FreshnessProbable,
            fallbackReason: "Index contract file was not found: ops.catalog.json.");
        var metadataResolver = new StubValidateMetadataResolver(ValidateMetadataResolutionResult.Success(
            RequestStaticValidationCatalog.Unavailable,
            readIndex));
        var unityIpcRequestExecutor = new SpyUnityIpcRequestExecutor(UnityIpcRequestExecutionResult.Success(
            CreateResponse(
                status: IpcProtocol.StatusOk,
                opResults: [],
                errors: [],
                planToken: "plan-token-1")));
        var service = new PlanService(
            new StubRequestPreparationService(ParsedRequestResult.Success(CreateParsedRequestContext())),
            new StubProjectContextResolver(ProjectContextResolutionResult.Success(CreateProjectContext())),
            metadataResolver,
            validator,
            unityIpcRequestExecutor);

        var result = await service.Execute(
            new PlanCommandInput(
                RequestPath: null,
                ProjectPath: "/repo/UnityProject",
                Mode: null,
                Timeout: null,
                ReadIndexMode: ReadIndexModeValues.AllowStale,
                FailFast: false),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Output);
        Assert.False(result.Output!.ReadIndex.Used);
        Assert.False(result.Output.ReadIndex.Hit);
        Assert.Contains("ops.catalog.json", result.Output.ReadIndex.FallbackReason, StringComparison.Ordinal);
        Assert.False(validator.LastCatalog!.IsAvailable);
        Assert.Equal(1, metadataResolver.CallCount);
        Assert.Equal(1, unityIpcRequestExecutor.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenRequireFreshMetadataResolutionFails_ReturnsFailureWithoutCallingUnity ()
    {
        var readIndex = CreateReadIndexInfo(
            used: true,
            hit: true,
            freshness: ReadIndexInfoTextCodec.FreshnessStale,
            fallbackReason: "readIndexMode=requireFresh requires index freshness 'fresh'.");
        var metadataResolver = new StubValidateMetadataResolver(ValidateMetadataResolutionResult.Failure(
            readIndex,
            IpcErrorCodes.ReadIndexFreshRequired,
            "readIndexMode=requireFresh requires index freshness 'fresh'."));
        var unityIpcRequestExecutor = new SpyUnityIpcRequestExecutor();
        var service = new PlanService(
            new StubRequestPreparationService(ParsedRequestResult.Success(CreateParsedRequestContext())),
            new StubProjectContextResolver(ProjectContextResolutionResult.Success(CreateProjectContext())),
            metadataResolver,
            new SpyRequestStaticValidator(ValidationResult.Success()),
            unityIpcRequestExecutor);

        var result = await service.Execute(
            new PlanCommandInput(
                RequestPath: null,
                ProjectPath: "/repo/UnityProject",
                Mode: null,
                Timeout: null,
                ReadIndexMode: ReadIndexModeValues.RequireFresh,
                FailFast: false),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal((int)CliExitCode.ToolError, result.ExitCode);
        Assert.NotNull(result.Output);
        Assert.Equal("9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62", result.Output!.RequestId);
        Assert.True(result.Output.ReadIndex.Used);
        Assert.True(result.Output.ReadIndex.Hit);
        Assert.Equal(ReadIndexInfoTextCodec.FreshnessStale, result.Output.ReadIndex.Freshness);
        Assert.Equal(0, unityIpcRequestExecutor.CallCount);
        var error = Assert.Single(result.Errors);
        Assert.Equal(IpcErrorCodes.ReadIndexFreshRequired, error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenStaticValidationFails_ReturnsInvalidArgumentWithoutCallingUnity ()
    {
        ValidationError[] validationErrors =
        [
            new ValidationError(
                ValidationErrorCodes.OperationArgsInvalid,
                "Operation args are invalid.",
                "step-1"),
        ];
        var unityIpcRequestExecutor = new SpyUnityIpcRequestExecutor();
        var service = new PlanService(
            new StubRequestPreparationService(ParsedRequestResult.Success(CreateParsedRequestContext())),
            new StubProjectContextResolver(ProjectContextResolutionResult.Success(CreateProjectContext())),
            new StubValidateMetadataResolver(CreateMetadataSuccessResult()),
            new SpyRequestStaticValidator(new ValidationResult(validationErrors)),
            unityIpcRequestExecutor);

        var result = await service.Execute(
            new PlanCommandInput(
                RequestPath: null,
                ProjectPath: "/repo/UnityProject",
                Mode: null,
                Timeout: null,
                ReadIndexMode: null,
                FailFast: false),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        Assert.NotNull(result.Output);
        Assert.Equal(0, unityIpcRequestExecutor.CallCount);
        var error = Assert.Single(result.Errors);
        Assert.Equal(ValidationErrorCodes.OperationArgsInvalid, error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenUnityResponseOmitsPlanToken_ReturnsInternalErrorWithPartialOpResults ()
    {
        var unityIpcRequestExecutor = new SpyUnityIpcRequestExecutor(UnityIpcRequestExecutionResult.Success(
            CreateResponse(
                status: IpcProtocol.StatusOk,
                opResults:
                [
                    new IpcExecuteOperationResult(
                        OpId: "step-1",
                        Op: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe,
                        Phase: IpcExecuteOperationPhaseNames.Plan,
                        Applied: false,
                        Changed: false,
                        Touched: []),
                ],
                errors: [],
                planToken: null)));
        var service = new PlanService(
            new StubRequestPreparationService(ParsedRequestResult.Success(CreateParsedRequestContext())),
            new StubProjectContextResolver(ProjectContextResolutionResult.Success(CreateProjectContext())),
            new StubValidateMetadataResolver(CreateMetadataSuccessResult()),
            new SpyRequestStaticValidator(ValidationResult.Success()),
            unityIpcRequestExecutor);

        var result = await service.Execute(
            new PlanCommandInput(
                RequestPath: null,
                ProjectPath: "/repo/UnityProject",
                Mode: null,
                Timeout: null,
                ReadIndexMode: null,
                FailFast: false),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal((int)CliExitCode.ToolError, result.ExitCode);
        Assert.NotNull(result.Output);
        Assert.Single(result.Output!.OpResults);
        Assert.Null(result.Output.PlanToken);
        var error = Assert.Single(result.Errors);
        Assert.Equal(IpcErrorCodes.InternalError, error.Code);
        Assert.Contains("planToken", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(IpcErrorCodes.EditorPlaymode)]
    [InlineData(CliErrorCodes.IpcTimeout)]
    public async Task Execute_WhenUnityExecutionFailsWithToolErrorCode_ReturnsToolErrorAndPreservesPayload (string errorCode)
    {
        var unityIpcRequestExecutor = new SpyUnityIpcRequestExecutor(UnityIpcRequestExecutionResult.Failure(
            "Unity execution failed.",
            errorCode));
        var service = new PlanService(
            new StubRequestPreparationService(ParsedRequestResult.Success(CreateParsedRequestContext())),
            new StubProjectContextResolver(ProjectContextResolutionResult.Success(CreateProjectContext())),
            new StubValidateMetadataResolver(CreateMetadataSuccessResult()),
            new SpyRequestStaticValidator(ValidationResult.Success()),
            unityIpcRequestExecutor);

        var result = await service.Execute(
            new PlanCommandInput(
                RequestPath: null,
                ProjectPath: "/repo/UnityProject",
                Mode: null,
                Timeout: null,
                ReadIndexMode: null,
                FailFast: false),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal((int)CliExitCode.ToolError, result.ExitCode);
        Assert.NotNull(result.Output);
        Assert.Equal("9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62", result.Output!.RequestId);
        Assert.NotNull(result.Output.ReadIndex);
        var error = Assert.Single(result.Errors);
        Assert.Equal(errorCode, error.Code);
    }

    private static ParsedRequestContext CreateParsedRequestContext ()
    {
        return new ParsedRequestContext(
            RequestJson: """
                {
                  "protocolVersion": 1,
                  "requestId": "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
                  "steps": []
                }
                """,
            InputSource: RequestInputSource.StandardInput,
            Request: new ValidateRequest(
                ProtocolVersion: 1,
                RequestId: "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
                Steps: Array.Empty<ValidateRequestStep?>()));
    }

    private static ProjectContext CreateProjectContext ()
    {
        return new ProjectContext(
            new ResolvedUnityProjectContext(
                UnityProjectRoot: "/repo/UnityProject",
                RepositoryRoot: "/repo",
                ProjectFingerprint: "project-fingerprint",
                PathSource: UnityProjectPathSource.CommandOption),
            UcliConfig.CreateDefault(),
            ConfigSource.Default);
    }

    private static ValidateMetadataResolutionResult CreateMetadataSuccessResult ()
    {
        return ValidateMetadataResolutionResult.Success(
            RequestStaticValidationCatalog.Available(
            [
                new UcliOperationDescriptor(
                    Name: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe,
                    Kind: UcliOperationKind.Query,
                    Policy: OperationPolicy.Safe,
                    ArgsSchemaJson: """{"type":"object"}"""),
            ]),
            CreateReadIndexInfo(
                used: true,
                hit: true,
                freshness: ReadIndexInfoTextCodec.FreshnessProbable,
                fallbackReason: null));
    }

    private static ReadIndexInfo CreateReadIndexInfo (
        bool used,
        bool hit,
        string freshness,
        string? fallbackReason)
    {
        return new ReadIndexInfo(
            Used: used,
            Hit: hit,
            Source: ReadIndexInfoTextCodec.SourceIndex,
            Freshness: freshness,
            GeneratedAtUtc: used
                ? DateTimeOffset.Parse("2026-03-06T00:00:00+00:00")
                : null,
            FallbackReason: fallbackReason);
    }

    private static IpcResponse CreateResponse (
        string status,
        IReadOnlyList<IpcExecuteOperationResult> opResults,
        IReadOnlyList<IpcError> errors,
        string? planToken)
    {
        return new IpcResponse(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: "req-1",
            Status: status,
            Payload: IpcPayloadCodec.SerializeToElement(new IpcExecuteResponse(opResults)
            {
                PlanToken = planToken,
            }),
            Errors: errors);
    }

    private sealed class StubRequestPreparationService : IRequestPreparationService
    {
        private readonly ParsedRequestResult readAndParseResult;

        public StubRequestPreparationService (ParsedRequestResult readAndParseResult)
        {
            this.readAndParseResult = readAndParseResult ?? throw new ArgumentNullException(nameof(readAndParseResult));
        }

        public ValueTask<ParsedRequestResult> ReadAndParse (
            string? requestPath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(readAndParseResult);
        }

        public ValueTask<RequestPreparationResult> Prepare (
            string? requestPath,
            string? projectPath,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubProjectContextResolver : IProjectContextResolver
    {
        private readonly ProjectContextResolutionResult result;

        public StubProjectContextResolver (ProjectContextResolutionResult result)
        {
            this.result = result ?? throw new ArgumentNullException(nameof(result));
        }

        public ValueTask<ProjectContextResolutionResult> Resolve (
            string? projectPath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(result);
        }
    }

    private sealed class StubValidateMetadataResolver : IValidateMetadataResolver
    {
        private readonly ValidateMetadataResolutionResult result;

        public StubValidateMetadataResolver (ValidateMetadataResolutionResult result)
        {
            this.result = result ?? throw new ArgumentNullException(nameof(result));
        }

        public int CallCount { get; private set; }

        public ValueTask<ValidateMetadataResolutionResult> Resolve (
            ResolvedUnityProjectContext unityProject,
            ReadIndexMode readIndexMode,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return ValueTask.FromResult(result);
        }
    }

    private sealed class SpyRequestStaticValidator : IRequestStaticValidator
    {
        private readonly ValidationResult result;

        public SpyRequestStaticValidator (ValidationResult result)
        {
            this.result = result ?? throw new ArgumentNullException(nameof(result));
        }

        public RequestStaticValidationCatalog? LastCatalog { get; private set; }

        public ValueTask<ValidationResult> Validate (
            ValidateRequest request,
            RequestStaticValidationCatalog catalog,
            UcliConfig config,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastCatalog = catalog;
            return ValueTask.FromResult(result);
        }
    }

    private sealed class SpyUnityIpcRequestExecutor : IUnityIpcRequestExecutor
    {
        private readonly Queue<UnityIpcRequestExecutionResult> results;

        private readonly List<Invocation> invocations = [];

        public SpyUnityIpcRequestExecutor (params UnityIpcRequestExecutionResult[] results)
        {
            this.results = new Queue<UnityIpcRequestExecutionResult>(results ?? throw new ArgumentNullException(nameof(results)));
        }

        public int CallCount { get; private set; }

        public UcliCommand CapturedCommand => invocations[^1].Command;

        public UnityExecutionMode CapturedMode => invocations[^1].Mode;

        public TimeSpan CapturedTimeout => invocations[^1].Timeout;

        public string? CapturedMethod => invocations[^1].Method;

        public JsonElement CapturedPayload => invocations[^1].Payload;

        public ValueTask<UnityIpcRequestExecutionResult> Execute (
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
            CallCount++;
            invocations.Add(new Invocation(command, mode, timeout, method, payload));
            if (!results.TryDequeue(out var result))
            {
                throw new InvalidOperationException("No queued Unity IPC execution result is available.");
            }

            return ValueTask.FromResult(result);
        }

        public sealed record Invocation (
            UcliCommand Command,
            UnityExecutionMode Mode,
            TimeSpan Timeout,
            string Method,
            JsonElement Payload);
    }
}