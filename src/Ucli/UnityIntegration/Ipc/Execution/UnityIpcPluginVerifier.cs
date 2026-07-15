using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityRequest;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.UnityIntegration.Ipc.Failures;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Execution;

/// <summary> Verifies that the target Unity project contains the uCLI Unity plugin within the shared execution budget. </summary>
internal sealed class UnityIpcPluginVerifier
{
    private readonly IUnityUcliPluginLocator unityUcliPluginLocator;

    /// <summary> Initializes a new instance of the <see cref="UnityIpcPluginVerifier" /> class. </summary>
    /// <param name="unityUcliPluginLocator"> The Unity plugin locator dependency. </param>
    public UnityIpcPluginVerifier (IUnityUcliPluginLocator unityUcliPluginLocator)
    {
        this.unityUcliPluginLocator = unityUcliPluginLocator ?? throw new ArgumentNullException(nameof(unityUcliPluginLocator));
    }

    /// <summary> Verifies the uCLI Unity plugin before a host path that requires plugin files. </summary>
    /// <param name="unityProjectRoot"> The Unity project root path. </param>
    /// <param name="deadline"> The shared execution deadline. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The classified failure when verification fails; otherwise <see langword="null" />. </returns>
    public async ValueTask<UnityRequestFailure?> VerifyWithinBudgetAsync (
        string unityProjectRoot,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(unityProjectRoot);
        ArgumentNullException.ThrowIfNull(deadline);
        cancellationToken.ThrowIfCancellationRequested();

        if (!deadline.TryGetRemainingTimeout(out var timeout))
        {
            return UnityIpcFailureClassifier.Timeout("Timed out before uCLI Unity plugin verification could begin.");
        }

        try
        {
            using var pluginLocateCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            pluginLocateCancellationTokenSource.CancelAfter(timeout);
            var pluginLocateResult = await unityUcliPluginLocator.LocateAsync(
                    unityProjectRoot,
                    pluginLocateCancellationTokenSource.Token)
                .ConfigureAwait(false);
            return pluginLocateResult.IsSuccess
                ? null
                : UnityIpcFailureClassifier.FromExecutionError(pluginLocateResult.Error!);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return UnityIpcFailureClassifier.Timeout(
                $"Timed out while verifying the uCLI Unity plugin. Timeout={timeout.TotalMilliseconds:0}ms.");
        }
        catch (Exception exception)
        {
            return UnityIpcFailureClassifier.FromExecutionError(ExecutionError.InternalError(
                $"Failed while verifying the uCLI Unity plugin. {exception.Message}"));
        }
    }
}
