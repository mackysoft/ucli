using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.Application.Shared.Execution.UnityRequest;

/// <summary> Represents one Unity request execution result. </summary>
internal sealed record UnityRequestExecutionResult
{
    private const string SuccessMessage = "Unity IPC request execution completed.";

    private UnityRequestExecutionResult (
        UnityRequestResponse? response,
        UnityRequestFailure? failure,
        LifecycleExecutionStartBinding? lifecycleExecutionStart,
        bool lifecycleActionDispatched,
        LifecycleExecutionHostExitObservation? confirmedHostExit)
    {
        if (lifecycleActionDispatched && lifecycleExecutionStart == null)
        {
            throw new ArgumentException(
                "A dispatched Lifecycle Execution action requires its durable start binding.",
                nameof(lifecycleActionDispatched));
        }
        if (confirmedHostExit is not null
            && (failure is null
                || lifecycleExecutionStart is null
                || confirmedHostExit.Process
                    != lifecycleExecutionStart.Host.Process))
        {
            throw new ArgumentException(
                "A confirmed Lifecycle Execution host exit requires a failed result retaining the same fixed process identity.",
                nameof(confirmedHostExit));
        }

        Response = response;
        FailureInfo = failure;
        LifecycleExecutionStart = lifecycleExecutionStart;
        LifecycleActionDispatched = lifecycleActionDispatched;
        ConfirmedHostExit = confirmedHostExit;
    }

    /// <summary> Gets the host-decoded response on success; otherwise <see langword="null" />. </summary>
    public UnityRequestResponse? Response { get; }

    /// <summary> Gets the classified failure on failure; otherwise <see langword="null" />. </summary>
    public UnityRequestFailure? FailureInfo { get; }

    /// <summary>
    /// Gets the durable Lifecycle Execution start binding when registration completed before
    /// response delivery or waiting failed.
    /// </summary>
    public LifecycleExecutionStartBinding? LifecycleExecutionStart { get; }

    /// <summary>
    /// Gets a value indicating whether the provider began the action request after its durable
    /// Lifecycle Execution start was confirmed.
    /// </summary>
    public bool LifecycleActionDispatched { get; }

    /// <summary>
    /// Gets the exact fixed-host exit observation when the registered process generation was
    /// confirmed dead; otherwise <see langword="null" />.
    /// </summary>
    public LifecycleExecutionHostExitObservation? ConfirmedHostExit { get; }

    /// <summary> Gets the user-facing result message. </summary>
    public string Message => FailureInfo?.Message ?? SuccessMessage;

    /// <summary> Gets the machine-readable error code on failure; otherwise <see langword="null" />. </summary>
    public UcliCode? ErrorCode => FailureInfo?.Code;

    /// <summary> Gets a value indicating whether request execution succeeded. </summary>
    public bool IsSuccess => Response is not null && FailureInfo is null;

    /// <summary> Retains a provider-confirmed start binding on this response or classified wait failure. </summary>
    public UnityRequestExecutionResult WithLifecycleExecutionStart (
        LifecycleExecutionStartBinding? lifecycleExecutionStart,
        bool lifecycleActionDispatched = false)
    {
        if (lifecycleExecutionStart == null)
        {
            if (lifecycleActionDispatched)
            {
                throw new ArgumentException(
                    "A dispatched Lifecycle Execution action requires its durable start binding.",
                    nameof(lifecycleActionDispatched));
            }

            return this;
        }

        if (LifecycleExecutionStart != null
            && LifecycleExecutionStart != lifecycleExecutionStart)
        {
            throw new InvalidOperationException(
                "Unity request result already retains a different Lifecycle Execution start binding.");
        }

        var retainedActionDispatched =
            LifecycleActionDispatched || lifecycleActionDispatched;
        return LifecycleExecutionStart == lifecycleExecutionStart
            && LifecycleActionDispatched == retainedActionDispatched
            ? this
            : new UnityRequestExecutionResult(
                Response,
                FailureInfo,
                lifecycleExecutionStart,
                retainedActionDispatched,
                ConfirmedHostExit);
    }

    /// <summary> Creates a successful request-execution result. </summary>
    /// <param name="response"> The host-decoded response returned from Unity. </param>
    /// <returns> The successful request-execution result. </returns>
    public static UnityRequestExecutionResult Success (
        UnityRequestResponse response,
        LifecycleExecutionStartBinding? lifecycleExecutionStart = null,
        bool lifecycleActionDispatched = false)
    {
        ArgumentNullException.ThrowIfNull(response);

        return new UnityRequestExecutionResult(
            response,
            failure: null,
            lifecycleExecutionStart,
            lifecycleActionDispatched,
            confirmedHostExit: null);
    }

    /// <summary> Creates a failed request-execution result. </summary>
    /// <param name="failure"> The classified request failure. </param>
    /// <returns> The failed request-execution result. </returns>
    public static UnityRequestExecutionResult Failure (
        UnityRequestFailure failure,
        LifecycleExecutionStartBinding? lifecycleExecutionStart = null,
        bool lifecycleActionDispatched = false,
        LifecycleExecutionHostExitObservation? confirmedHostExit = null)
    {
        ArgumentNullException.ThrowIfNull(failure);

        return new UnityRequestExecutionResult(
            response: null,
            failure,
            lifecycleExecutionStart,
            lifecycleActionDispatched,
            confirmedHostExit);
    }
}
