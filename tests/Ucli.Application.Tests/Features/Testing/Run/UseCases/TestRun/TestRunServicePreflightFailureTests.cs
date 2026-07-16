using MackySoft.Ucli.Application.Features.Testing.Run.Configuration;
using MackySoft.Ucli.Application.Features.Testing.Run.Execution;
using MackySoft.Ucli.Application.Features.Testing.Run.Results;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Foundation;
using static MackySoft.Ucli.Application.Tests.TestRunServiceTestFactory;

namespace MackySoft.Ucli.Application.Tests;

public sealed class TestRunServicePreflightFailureTests
{
    public static TheoryData<UcliCode, string> ModeContractErrorCases => new()
    {
        {
            UnityExecutionModeDecisionErrorCodes.DaemonNotRunning,
            "Daemon is not running for mode=daemon."
        },
        {
            UnityExecutionModeDecisionErrorCodes.DaemonRunningOneshotForbidden,
            "Daemon is running for mode=oneshot."
        },
    };

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithConfigurationInvalidArgumentFailure_ReturnsInvalidInput ()
    {
        var service = CreateService(
            configurationResolver: new StubTestRunConfigurationResolver(
                TestRunConfigurationResolutionResult.Failure(
                [
                    ExecutionError.InvalidArgument("testPlatform must be editmode, playmode, or a Unity BuildTarget literal."),
                ])),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Oneshot, false, UnityExecutionTarget.Oneshot, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubTestRunArtifactsService(
                prepare: _ => throw new InvalidOperationException(),
                complete: (_, _, _) => throw new InvalidOperationException()),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) => ValueTask.FromResult(UnityTestExecutionResult.Success(0))),
            resultsConverter: new StubUnityResultsConverter(_ => ValueTask.FromResult(UnityResultsConversionResult.Success(false))));

        var result = await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None);

        Assert.Null(result.Result);
        Assert.Equal(TestRunErrorKind.InvalidInput, result.ErrorKind);
        Assert.Equal(ApplicationOutcome.InvalidArgument, result.Outcome);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, result.ErrorCode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithConfigDiagnostics_ReturnsInvalidInput ()
    {
        var configuration = CreateResolvedConfiguration();
        var service = CreateService(
            configurationResolver: new StubTestRunConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            configStore: new StubUcliConfigStore(UcliConfigLoadResult.Failure(
            [
                UcliConfigDiagnostic.Create(
                    "config.semantic.unsupportedLiteral",
                    "operationPolicy",
                    "config.json",
                    "Config operationPolicy is invalid: unsupported."),
                UcliConfigDiagnostic.Create(
                    "config.semantic.unsupportedLiteral",
                    "planTokenMode",
                    "config.json",
                    "Config planTokenMode is invalid: never."),
            ])),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Oneshot, false, UnityExecutionTarget.Oneshot, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubTestRunArtifactsService(
                prepare: _ => throw new InvalidOperationException(),
                complete: (_, _, _) => throw new InvalidOperationException()),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) => ValueTask.FromResult(UnityTestExecutionResult.Success(0))),
            resultsConverter: new StubUnityResultsConverter(_ => ValueTask.FromResult(UnityResultsConversionResult.Success(false))));

        var result = await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None);

        Assert.Null(result.Result);
        Assert.Equal(TestRunErrorKind.InvalidInput, result.ErrorKind);
        Assert.Equal(ApplicationOutcome.InvalidArgument, result.Outcome);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, result.ErrorCode);
        Assert.Contains("operationPolicy", result.Message, StringComparison.Ordinal);
        Assert.Contains("planTokenMode", result.Message, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(ModeContractErrorCases))]
    public async Task Execute_WithModeContractError_ReturnsToolErrorWithModeCode (
        UcliCode expectedErrorCode,
        string message)
    {
        var configuration = CreateResolvedConfiguration();

        var service = CreateService(
            configurationResolver: new StubTestRunConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.ContractFailure(
                new UnityExecutionModeDecisionContractError(
                    expectedErrorCode,
                    message))),
            artifactsService: new StubTestRunArtifactsService(
                prepare: _ => throw new InvalidOperationException(),
                complete: (_, _, _) => throw new InvalidOperationException()),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) => ValueTask.FromResult(UnityTestExecutionResult.Success(0))),
            resultsConverter: new StubUnityResultsConverter(_ => ValueTask.FromResult(UnityResultsConversionResult.Success(false))));

        var result = await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None);

        Assert.Null(result.Result);
        Assert.Equal(TestRunErrorKind.ToolError, result.ErrorKind);
        Assert.Equal(ApplicationOutcome.ToolError, result.Outcome);
        Assert.Equal(expectedErrorCode, result.ErrorCode);
    }
}
