using System.Text.Json;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Validation;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;

namespace MackySoft.Ucli.Application.Tests;

public sealed class OperationExecutionResultContractValidatorTests
{
    private const string OperationName = "test.judge";

    private static readonly Sha256Digest DescriptorDigest = Sha256Digest.Compute("test.judge descriptor"u8);

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidate_WhenSuccessfulJudgingCallMatchesPreparedContract_AcceptsResult ()
    {
        var descriptor = CreateJudgingDescriptor("""{"type":"object"}""");
        var response = CreateResponse(
            CreateJudgingCallResult(
                digest: DescriptorDigest,
                result: JsonSerializer.SerializeToElement(new
                {
                    satisfied = true,
                }),
                verdict: Verdict.Pass));

        var isValid = OperationExecutionResultContractValidator.TryValidate(
            CreateRequest(),
            CreateOperations(descriptor),
            IpcExecuteOperationPhase.Call,
            response,
            out var errorMessage);

        Assert.True(isValid, errorMessage);
        Assert.Null(errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidate_WhenSuccessfulResponseDoesNotContainOneResultPerRequestStep_RejectsResponse ()
    {
        var isValid = OperationExecutionResultContractValidator.TryValidate(
            CreateRequest(),
            CreateOperations(CreateResultlessDescriptor()),
            IpcExecuteOperationPhase.Call,
            CreateResponse(),
            out var errorMessage);

        Assert.False(isValid);
        Assert.Contains("'opResults' field contains 0 items", errorMessage, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidate_WhenFailedResponseContainsValidPrefixResults_AcceptsResponse ()
    {
        var request = new ValidateRequest(
            IpcProtocol.CurrentVersion,
            [
                new ValidateRequestStep(
                    IpcExecuteStepKind.Op,
                    StepIndex: 0,
                    Op: OperationName,
                    Args: JsonSerializer.SerializeToElement(new { })),
                new ValidateRequestStep(
                    IpcExecuteStepKind.Op,
                    StepIndex: 1,
                    Op: OperationName,
                    Args: JsonSerializer.SerializeToElement(new { })),
            ],
            AllowPlayMode: false);
        var response = CreateResponse(
            [
                CreateResultWithoutVerdict(
                    phase: IpcExecuteOperationPhase.Validate,
                    digest: DescriptorDigest,
                    result: null),
            ],
            errors:
            [
                new OperationExecutionError(
                    UcliCoreErrorCodes.InvalidArgument,
                    "Validation failed.",
                    "/steps/0"),
            ]);

        var isValid = OperationExecutionResultContractValidator.TryValidate(
            request,
            CreateOperations(CreateResultlessDescriptor()),
            IpcExecuteOperationPhase.Call,
            response,
            out var errorMessage);

        Assert.True(isValid, errorMessage);
        Assert.Null(errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidate_WhenSuccessfulResultPhaseDoesNotMatchExecutedPass_RejectsResponse ()
    {
        var response = CreateResponse(CreateResultWithoutVerdict(
            phase: IpcExecuteOperationPhase.Plan,
            digest: DescriptorDigest,
            result: null));

        var isValid = OperationExecutionResultContractValidator.TryValidate(
            CreateRequest(),
            CreateOperations(CreateResultlessDescriptor()),
            IpcExecuteOperationPhase.Call,
            response,
            out var errorMessage);

        Assert.False(isValid);
        Assert.Contains("'opResults[0].phase' field does not match the executed pass", errorMessage, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidateDirectOperation_WhenSuccessfulResultPhaseDoesNotMatchExecutedPass_RejectsResponse ()
    {
        var isValid = OperationExecutionResultContractValidator.TryValidateDirectOperation(
            CreateResultlessDescriptor(),
            IpcExecuteOperationPhase.Call,
            CreateResponse(CreateResultWithoutVerdict(
                phase: IpcExecuteOperationPhase.Plan,
                digest: DescriptorDigest,
                result: null)),
            out var errorMessage);

        Assert.False(isValid);
        Assert.Contains("'opResults[0].phase' field does not match the executed pass", errorMessage, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidate_WhenPartialFailureReportsAnotherStepName_RejectsResponse ()
    {
        var response = CreateResponse(
            [
                CreateNamedResultWithoutVerdict(
                    operationName: "test.other",
                    phase: IpcExecuteOperationPhase.Validate,
                    digest: DescriptorDigest),
            ],
            errors:
            [
                new OperationExecutionError(
                    UcliCoreErrorCodes.InvalidArgument,
                    "Validation failed.",
                    "/steps/0"),
            ]);

        var isValid = OperationExecutionResultContractValidator.TryValidate(
            CreateRequest(),
            CreateOperations(CreateResultlessDescriptor()),
            IpcExecuteOperationPhase.Call,
            response,
            out var errorMessage);

        Assert.False(isValid);
        Assert.Contains("'opResults[0].op' field does not match", errorMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Size", "Small")]
    public void TryValidate_WhenDirectResultHasNoMatchingDescriptorDigest_RejectsResponse (bool omitDigest)
    {
        var resultDigest = omitDigest
            ? null
            : Sha256Digest.Compute("different descriptor"u8);
        var response = CreateResponse(CreateResultWithoutVerdict(
            phase: IpcExecuteOperationPhase.Call,
            digest: resultDigest,
            result: null));

        var isValid = OperationExecutionResultContractValidator.TryValidate(
            CreateRequest(),
            CreateOperations(CreateResultlessDescriptor()),
            IpcExecuteOperationPhase.Call,
            response,
            out var errorMessage);

        Assert.False(isValid);
        Assert.Contains("'opResults[0].operationDescriptorDigest' field does not match", errorMessage, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidate_WhenOperationReturnsResultWithoutResultContract_RejectsResponse ()
    {
        var response = CreateResponse(CreateResultWithoutVerdict(
            phase: IpcExecuteOperationPhase.Call,
            digest: DescriptorDigest,
            result: JsonSerializer.SerializeToElement(new
            {
                value = 1,
            })));

        var isValid = OperationExecutionResultContractValidator.TryValidate(
            CreateRequest(),
            CreateOperations(CreateResultlessDescriptor()),
            IpcExecuteOperationPhase.Call,
            response,
            out var errorMessage);

        Assert.False(isValid);
        Assert.Contains("without a result contract", errorMessage, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidate_WhenRegisteredResultSchemaCannotBeBuilt_RejectsResponse ()
    {
        var descriptor = CreateResultDescriptor("""{"type":""");
        var response = CreateResponse(CreateResultWithoutVerdict(
            phase: IpcExecuteOperationPhase.Call,
            digest: DescriptorDigest,
            result: JsonSerializer.SerializeToElement(true)));

        var isValid = OperationExecutionResultContractValidator.TryValidate(
            CreateRequest(),
            CreateOperations(descriptor),
            IpcExecuteOperationPhase.Call,
            response,
            out var errorMessage);

        Assert.False(isValid);
        Assert.Contains("could not be evaluated", errorMessage, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidate_WhenSuccessfulJudgingCallOmitsVerdict_RejectsResponse ()
    {
        var descriptor = CreateJudgingDescriptor("""{"type":"boolean"}""");
        var response = CreateResponse(CreateResultWithoutVerdict(
            phase: IpcExecuteOperationPhase.Call,
            digest: DescriptorDigest,
            result: JsonSerializer.SerializeToElement(true)));

        var isValid = OperationExecutionResultContractValidator.TryValidate(
            CreateRequest(),
            CreateOperations(descriptor),
            IpcExecuteOperationPhase.Call,
            response,
            out var errorMessage);

        Assert.False(isValid);
        Assert.Contains("completed its Call without a verdict", errorMessage, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidate_WhenSuccessfulResultfulCallOmitsResult_RejectsResponse ()
    {
        var descriptor = CreateResultDescriptor("""{"type":"boolean"}""");
        var response = CreateResponse(CreateResultWithoutVerdict(
            phase: IpcExecuteOperationPhase.Call,
            digest: DescriptorDigest,
            result: null));

        var isValid = OperationExecutionResultContractValidator.TryValidate(
            CreateRequest(),
            CreateOperations(descriptor),
            IpcExecuteOperationPhase.Call,
            response,
            out var errorMessage);

        Assert.False(isValid);
        Assert.Contains("Resultful operation", errorMessage, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidate_WhenNonJudgingOperationReturnsVerdict_RejectsResponse ()
    {
        var descriptor = CreateResultDescriptor("""{"type":"boolean"}""");
        var response = CreateResponse(CreateJudgingCallResult(
            digest: DescriptorDigest,
            result: JsonSerializer.SerializeToElement(true),
            verdict: Verdict.Fail));

        var isValid = OperationExecutionResultContractValidator.TryValidate(
            CreateRequest(),
            CreateOperations(descriptor),
            IpcExecuteOperationPhase.Call,
            response,
            out var errorMessage);

        Assert.False(isValid);
        Assert.Contains("'opResults[0].verdict' field is not valid", errorMessage, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidate_WhenFailedCallCarriesVerdict_RejectsResponse ()
    {
        var descriptor = CreateJudgingDescriptor("""{"type":"boolean"}""");
        var result = CreateJudgingCallResult(
            digest: DescriptorDigest,
            result: JsonSerializer.SerializeToElement(true),
            verdict: Verdict.Pass);
        var errors = new[]
        {
            new OperationExecutionError(
                UcliCoreErrorCodes.InternalError,
                "Execution failed.",
                "/steps/0"),
        };

        var isValid = OperationExecutionResultContractValidator.TryValidate(
            CreateRequest(),
            CreateOperations(descriptor),
            IpcExecuteOperationPhase.Call,
            CreateResponse([result], errors),
            out var errorMessage);

        Assert.False(isValid);
        Assert.Contains("'opResults[0].verdict' field is not valid", errorMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Size", "Small")]
    public void TryValidate_WhenLaterOperationFails_ValidatesEarlierJudgingCallIndependently (
        bool tamperWithEarlierResult)
    {
        const string laterOperationName = "test.later";
        var laterDescriptorDigest = Sha256Digest.Compute("test.later descriptor"u8);
        var judgingDescriptor = CreateJudgingDescriptor("""{"type":"boolean"}""");
        var laterDescriptor = new UcliOperationDescriptor(
            laterOperationName,
            UcliOperationKind.Command,
            OperationPolicy.Safe,
            ArgsSchemaJson: """{"type":"object"}""",
            ResultSchemaJson: null,
            DescriptorDigest: laterDescriptorDigest,
            VerdictContract: null,
            Exposure: UcliOperationExposure.Public);
        var request = new ValidateRequest(
            IpcProtocol.CurrentVersion,
            [
                new ValidateRequestStep(
                    IpcExecuteStepKind.Op,
                    StepIndex: 0,
                    Op: OperationName,
                    Args: JsonSerializer.SerializeToElement(new { })),
                new ValidateRequestStep(
                    IpcExecuteStepKind.Op,
                    StepIndex: 1,
                    Op: laterOperationName,
                    Args: JsonSerializer.SerializeToElement(new { })),
            ],
            AllowPlayMode: false);
        var response = CreateResponse(
            [
                tamperWithEarlierResult
                    ? CreateResultWithoutVerdict(
                        phase: IpcExecuteOperationPhase.Plan,
                        digest: DescriptorDigest,
                        result: JsonSerializer.SerializeToElement(true))
                    : CreateJudgingCallResult(
                        digest: DescriptorDigest,
                        result: JsonSerializer.SerializeToElement(true),
                        verdict: Verdict.Pass),
                CreateNamedResultWithoutVerdict(
                    operationName: laterOperationName,
                    phase: IpcExecuteOperationPhase.Call,
                    digest: laterDescriptorDigest),
            ],
            errors:
            [
                new OperationExecutionError(
                    UcliCoreErrorCodes.InternalError,
                    "The later operation failed.",
                    "/steps/1"),
            ]);

        var isValid = OperationExecutionResultContractValidator.TryValidate(
            request,
            new Dictionary<string, UcliOperationDescriptor>(StringComparer.Ordinal)
            {
                [OperationName] = judgingDescriptor,
                [laterOperationName] = laterDescriptor,
            },
            IpcExecuteOperationPhase.Call,
            response,
            out var errorMessage);

        Assert.Equal(!tamperWithEarlierResult, isValid);
        if (tamperWithEarlierResult)
        {
            Assert.Contains(
                "'opResults[0].phase' field does not match the executed pass",
                errorMessage,
                StringComparison.Ordinal);
        }
        else
        {
            Assert.Null(errorMessage);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidate_WhenEditResultCarriesDescriptorDigest_RejectsResponse ()
    {
        var editRequest = new ValidateRequest(
            IpcProtocol.CurrentVersion,
            [
                new ValidateRequestStep(
                    IpcExecuteStepKind.Edit,
                    StepIndex: 0,
                    Op: null,
                    Args: default),
            ],
            AllowPlayMode: false);
        var editResult = OperationExecutionOperationResult.CreateWithoutVerdict(
            TextVocabulary.GetText(IpcExecuteStepKind.Edit),
            IpcExecuteOperationPhase.Call,
            applied: true,
            changed: true,
            touched: [],
            operationDescriptorDigest: DescriptorDigest,
            result: null,
            diagnostics: []);

        var isValid = OperationExecutionResultContractValidator.TryValidate(
            editRequest,
            new Dictionary<string, UcliOperationDescriptor>(),
            IpcExecuteOperationPhase.Call,
            CreateResponse(editResult),
            out var errorMessage);

        Assert.False(isValid);
        Assert.Contains(
            "'opResults[0].operationDescriptorDigest' field must be null for an Edit step",
            errorMessage,
            StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidate_WhenEditResultCarriesOperationResult_RejectsResponse ()
    {
        var editRequest = new ValidateRequest(
            IpcProtocol.CurrentVersion,
            [
                new ValidateRequestStep(
                    IpcExecuteStepKind.Edit,
                    StepIndex: 0,
                    Op: null,
                    Args: default),
            ],
            AllowPlayMode: false);
        var editResult = OperationExecutionOperationResult.CreateWithoutVerdict(
            TextVocabulary.GetText(IpcExecuteStepKind.Edit),
            IpcExecuteOperationPhase.Call,
            applied: true,
            changed: true,
            touched: [],
            operationDescriptorDigest: null,
            result: JsonSerializer.SerializeToElement(true),
            diagnostics: []);

        var isValid = OperationExecutionResultContractValidator.TryValidate(
            editRequest,
            new Dictionary<string, UcliOperationDescriptor>(),
            IpcExecuteOperationPhase.Call,
            CreateResponse(editResult),
            out var errorMessage);

        Assert.False(isValid);
        Assert.Contains("'opResults[0].result' field must be null for an Edit step", errorMessage, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidate_WhenEditResultCarriesVerdict_RejectsResponse ()
    {
        var editRequest = new ValidateRequest(
            IpcProtocol.CurrentVersion,
            [
                new ValidateRequestStep(
                    IpcExecuteStepKind.Edit,
                    StepIndex: 0,
                    Op: null,
                    Args: default),
            ],
            AllowPlayMode: false);
        var editResult = OperationExecutionOperationResult.CreateJudgingCallResult(
            TextVocabulary.GetText(IpcExecuteStepKind.Edit),
            applied: true,
            changed: true,
            touched: [],
            operationDescriptorDigest: DescriptorDigest,
            verdict: Verdict.Pass,
            result: JsonSerializer.SerializeToElement(true),
            diagnostics: []);

        var isValid = OperationExecutionResultContractValidator.TryValidate(
            editRequest,
            new Dictionary<string, UcliOperationDescriptor>(),
            IpcExecuteOperationPhase.Call,
            CreateResponse(editResult),
            out var errorMessage);

        Assert.False(isValid);
        Assert.Contains("'opResults[0].verdict' field must be null for an Edit step", errorMessage, StringComparison.Ordinal);
    }

    private static ValidateRequest CreateRequest ()
    {
        return new ValidateRequest(
            IpcProtocol.CurrentVersion,
            [
                new ValidateRequestStep(
                    IpcExecuteStepKind.Op,
                    StepIndex: 0,
                    Op: OperationName,
                    Args: JsonSerializer.SerializeToElement(new { })),
            ],
            AllowPlayMode: false);
    }

    private static UcliOperationDescriptor CreateResultlessDescriptor () =>
        CreateDescriptor(
            resultSchemaJson: null,
            verdictContract: null);

    private static UcliOperationDescriptor CreateResultDescriptor (string resultSchemaJson) =>
        CreateDescriptor(
            resultSchemaJson,
            verdictContract: null);

    private static UcliOperationDescriptor CreateJudgingDescriptor (string resultSchemaJson) =>
        CreateDescriptor(
            resultSchemaJson,
            new UcliOperationVerdictContract("The requested condition is satisfied."));

    private static UcliOperationDescriptor CreateDescriptor (
        string? resultSchemaJson,
        UcliOperationVerdictContract? verdictContract)
    {
        return new UcliOperationDescriptor(
            OperationName,
            UcliOperationKind.Query,
            OperationPolicy.Safe,
            ArgsSchemaJson: """{"type":"object"}""",
            ResultSchemaJson: resultSchemaJson,
            DescriptorDigest: DescriptorDigest,
            VerdictContract: verdictContract,
            Exposure: UcliOperationExposure.Public);
    }

    private static IReadOnlyDictionary<string, UcliOperationDescriptor> CreateOperations (
        UcliOperationDescriptor descriptor)
    {
        return new Dictionary<string, UcliOperationDescriptor>(StringComparer.Ordinal)
        {
            [descriptor.Name] = descriptor,
        };
    }

    private static OperationExecutionOperationResult CreateResultWithoutVerdict (
        IpcExecuteOperationPhase phase,
        Sha256Digest? digest,
        JsonElement? result)
    {
        return OperationExecutionOperationResult.CreateWithoutVerdict(
            OperationName,
            phase,
            applied: phase == IpcExecuteOperationPhase.Call,
            changed: false,
            touched: [],
            operationDescriptorDigest: digest,
            result,
            diagnostics: []);
    }

    private static OperationExecutionOperationResult CreateNamedResultWithoutVerdict (
        string operationName,
        IpcExecuteOperationPhase phase,
        Sha256Digest? digest)
    {
        return OperationExecutionOperationResult.CreateWithoutVerdict(
            operationName,
            phase,
            applied: phase == IpcExecuteOperationPhase.Call,
            changed: false,
            touched: [],
            operationDescriptorDigest: digest,
            result: null,
            diagnostics: []);
    }

    private static OperationExecutionOperationResult CreateJudgingCallResult (
        Sha256Digest digest,
        JsonElement result,
        Verdict verdict)
    {
        return OperationExecutionOperationResult.CreateJudgingCallResult(
            OperationName,
            applied: true,
            changed: false,
            touched: [],
            operationDescriptorDigest: digest,
            verdict,
            result,
            diagnostics: []);
    }

    private static ExecuteResponseConversionResult CreateResponse (
        params OperationExecutionOperationResult[] results)
    {
        return CreateResponse(results, []);
    }

    private static ExecuteResponseConversionResult CreateResponse (
        IReadOnlyList<OperationExecutionOperationResult> results,
        IReadOnlyList<OperationExecutionError> errors)
    {
        return new ExecuteResponseConversionResult(
            results,
            errors,
            ContractViolations: [],
            PlanToken: null,
            ReadPostcondition: null,
            PostReadSource: null,
            Project: null);
    }
}
