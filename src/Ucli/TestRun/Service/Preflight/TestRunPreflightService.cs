using MackySoft.Ucli.Cli;
using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.TestRun.Configuration;
using MackySoft.Ucli.TestRun.Service.Mapping;

namespace MackySoft.Ucli.TestRun.Service.Preflight;

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
    /// <param name="input"> The raw command input values. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by caller. </param>
    /// <returns> A task that resolves to preflight result values. </returns>
    public async ValueTask<TestRunPreflightResult> Execute (
        TestRunCommandInput input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(input);

        var configurationResolutionResult = await ResolveConfigurationSafely(input, cancellationToken).ConfigureAwait(false);
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

        var modeDecisionResult = await modeDecisionService.Decide(
            commandName: UcliCommandNames.Test,
            mode: configuration.Mode,
            timeout: null,
            config: configLoadResult.Config!,
            unityProject: configuration.UnityProject,
            cancellationToken).ConfigureAwait(false);
        if (modeDecisionResult.HasContractError)
        {
            return TestRunPreflightResult.FailureResult(TestRunServiceResult.InfraError(
                modeDecisionResult.ContractError!.Message,
                modeDecisionResult.ContractError.Code));
        }

        if (!modeDecisionResult.IsSuccess)
        {
            return TestRunPreflightResult.FailureResult(
                TestRunServiceErrorMapper.MapExecutionError(modeDecisionResult.Error!));
        }

        if (modeDecisionResult.Decision!.Target == UnityExecutionTarget.Daemon)
        {
            return TestRunPreflightResult.FailureResult(TestRunServiceResult.ToolError(
                "Daemon path is not supported by test run core service.",
                TestRunErrorCodes.TestRunDaemonPathUnsupported));
        }

        return TestRunPreflightResult.Success(configuration);
    }

    /// <summary> Resolves run configuration and converts unexpected exceptions into structured failures. </summary>
    /// <param name="input"> The raw command input values. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by caller. </param>
    /// <returns> A task that resolves to the configuration resolution result. </returns>
    private async ValueTask<TestRunConfigurationResolutionResult> ResolveConfigurationSafely (
        TestRunCommandInput input,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await configurationResolver.Resolve(input, cancellationToken).ConfigureAwait(false);
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

}