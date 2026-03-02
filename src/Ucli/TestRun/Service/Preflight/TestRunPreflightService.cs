using MackySoft.Ucli.Cli;
using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.TestRun.Configuration;

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

        var configurationResolutionResult = ResolveConfigurationSafely(input);
        if (!configurationResolutionResult.IsSuccess)
        {
            return TestRunPreflightResult.FailureResult(
                CreateConfigurationFailureResult(configurationResolutionResult.Errors));
        }

        var configuration = configurationResolutionResult.Configuration!;
        var configLoadResult = await configStore.Load(
            configuration.UnityProject.RepositoryRoot,
            cancellationToken).ConfigureAwait(false);
        if (!configLoadResult.IsSuccess)
        {
            return TestRunPreflightResult.FailureResult(
                CreateErrorFromExecutionError(configLoadResult.Error!));
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
                CreateErrorFromExecutionError(modeDecisionResult.Error!));
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
    /// <returns> The configuration resolution result. </returns>
    private TestRunConfigurationResolutionResult ResolveConfigurationSafely (TestRunCommandInput input)
    {
        try
        {
            return configurationResolver.Resolve(input);
        }
        catch (Exception exception)
        {
            return TestRunConfigurationResolutionResult.Failure(
            [
                ExecutionError.InternalError($"Unexpected error while resolving run configuration: {exception.Message}"),
            ]);
        }
    }

    /// <summary> Creates configuration resolution failure output from structured errors. </summary>
    /// <param name="errors"> The configuration resolution errors. </param>
    /// <returns> The failure service result. </returns>
    private static TestRunServiceResult CreateConfigurationFailureResult (IReadOnlyList<ExecutionError> errors)
    {
        if (errors.Count == 0)
        {
            return TestRunServiceResult.InfraError(
                "Unexpected error while resolving run configuration.",
                IpcErrorCodes.InternalError);
        }

        var hasInternalError = errors.Any(static error => error.Kind == ExecutionErrorKind.InternalError);
        var errorCode = hasInternalError
            ? IpcErrorCodes.InternalError
            : IpcErrorCodes.InvalidArgument;
        var message = string.Join(" | ", errors.Select(static error => error.Message));

        return hasInternalError
            ? TestRunServiceResult.InfraError(message, errorCode)
            : TestRunServiceResult.InvalidInput(message, errorCode);
    }

    /// <summary> Converts execution errors into service results. </summary>
    /// <param name="error"> The execution error. </param>
    /// <returns> The mapped service result. </returns>
    private static TestRunServiceResult CreateErrorFromExecutionError (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return error.Kind switch
        {
            ExecutionErrorKind.InvalidArgument => TestRunServiceResult.InvalidInput(
                error.Message,
                IpcErrorCodes.InvalidArgument),
            ExecutionErrorKind.Timeout => TestRunServiceResult.ToolError(
                error.Message,
                CliErrorCodes.IpcTimeout),
            _ => TestRunServiceResult.InfraError(
                error.Message,
                IpcErrorCodes.InternalError),
        };
    }
}