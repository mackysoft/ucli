using MackySoft.Ucli.Application.Features.Requests.Shared.Execution;
using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Application.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Application.Features.Testing.Run.Common.Contracts;
using MackySoft.Ucli.Application.Features.Testing.Run.Configuration;
using MackySoft.Ucli.Application.Features.Testing.Run.UseCases.TestRun.Projection;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.Process;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts;

namespace MackySoft.Ucli.Application.Features.Testing.Run.UseCases.TestRun.Preflight;

/// <summary> Implements preflight flow for configuration resolution and execution-mode decision. </summary>
internal sealed class TestRunPreflightService : ITestRunPreflightService
{
    private readonly ITestRunConfigurationResolver configurationResolver;

    private readonly IUcliConfigStore configStore;

    private readonly IUnityExecutionModeDecisionService modeDecisionService;

    /// <summary> Initializes a new instance of the <see cref="TestRunPreflightService" /> class. </summary>
    /// <param name="configurationResolver"> The test-run configuration resolver dependency. </param>
    /// <param name="configStore"> The uCLI config store dependency. </param>
    /// <param name="modeDecisionService"> The Unity execution mode decision service dependency. </param>
    public TestRunPreflightService (
        ITestRunConfigurationResolver configurationResolver,
        IUcliConfigStore configStore,
        IUnityExecutionModeDecisionService modeDecisionService)
    {
        this.configurationResolver = configurationResolver ?? throw new ArgumentNullException(nameof(configurationResolver));
        this.configStore = configStore ?? throw new ArgumentNullException(nameof(configStore));
        this.modeDecisionService = modeDecisionService ?? throw new ArgumentNullException(nameof(modeDecisionService));
    }

    /// <summary> Executes one preflight flow and returns either resolved configuration or failure output. </summary>
    /// <param name="input"> The interpreted command input values. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by caller. </param>
    /// <returns> A task that resolves to preflight result values. </returns>
    public async ValueTask<TestRunPreflightResult> Execute (
        TestRunCommandInput input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(input);

        var configurationResolutionResult = await ResolveConfigurationSafely(
            CreateConfigurationRequest(input),
            cancellationToken).ConfigureAwait(false);
        if (!configurationResolutionResult.IsSuccess)
        {
            return TestRunPreflightResult.FailureResult(
                TestRunServiceErrorMapper.MapConfigurationErrors(configurationResolutionResult.Errors));
        }

        var configuration = configurationResolutionResult.Configuration!;
        var configLoadResult = await configStore.Load(
            configuration.UnityProject.RepositoryRoot,
            cancellationToken).ConfigureAwait(false);
        if (!configLoadResult.IsSuccess)
        {
            return TestRunPreflightResult.FailureResult(
                TestRunServiceErrorMapper.MapExecutionError(configLoadResult.Error!));
        }

        var timeoutResolutionResult = IpcCommandTimeoutResolver.ResolveNormalized(
            configuration.TimeoutMilliseconds,
            UcliCommandIds.Test,
            configLoadResult.Config!);
        if (!timeoutResolutionResult.IsSuccess)
        {
            return TestRunPreflightResult.FailureResult(
                TestRunServiceErrorMapper.MapExecutionError(timeoutResolutionResult.Error!));
        }

        var modeDecisionResult = await modeDecisionService.Decide(
            mode: configuration.Mode,
            unityProject: configuration.UnityProject,
            timeout: timeoutResolutionResult.Timeout!.Value,
            cancellationToken).ConfigureAwait(false);
        if (modeDecisionResult.HasContractError)
        {
            return TestRunPreflightResult.FailureResult(TestRunServiceResult.ToolError(
                modeDecisionResult.ContractError!.Message,
                modeDecisionResult.ContractError.Code));
        }

        if (!modeDecisionResult.IsSuccess)
        {
            return TestRunPreflightResult.FailureResult(
                TestRunServiceErrorMapper.MapExecutionError(modeDecisionResult.Error!));
        }

        var context = new TestRunExecutionContext(
            Configuration: configuration,
            Target: modeDecisionResult.Decision!.Target,
            Timeout: timeoutResolutionResult.Timeout!.Value,
            FailFast: input.FailFast);
        return TestRunPreflightResult.Success(context);
    }

    /// <summary> Resolves run configuration and converts unexpected exceptions into structured failures. </summary>
    /// <param name="input"> The interpreted command input values. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by caller. </param>
    /// <returns> A task that resolves to the configuration resolution result. </returns>
    /// <exception cref="OperationCanceledException"> Thrown when <paramref name="cancellationToken" /> is canceled during resolution. </exception>
    private async ValueTask<TestRunConfigurationResolutionResult> ResolveConfigurationSafely (
        TestRunConfigurationRequest input,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await configurationResolver.ResolveAsync(input, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return TestRunConfigurationResolutionResult.Failure(
            [
                ExecutionError.InternalError($"Unexpected error while resolving run configuration: {exception.Message}"),
            ]);
        }
    }

    private static TestRunConfigurationRequest CreateConfigurationRequest (TestRunCommandInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        return new TestRunConfigurationRequest(
            ProjectPath: input.ProjectPath,
            ProfilePath: input.ProfilePath,
            Mode: input.Mode,
            UnityVersion: input.UnityVersion,
            UnityEditorPath: input.UnityEditorPath,
            TestPlatform: input.TestPlatform,
            TestFilter: input.TestFilter,
            TestCategory: input.TestCategory,
            AssemblyName: input.AssemblyName,
            TestSettingsPath: input.TestSettingsPath,
            TimeoutMilliseconds: input.TimeoutMilliseconds);
    }

}
