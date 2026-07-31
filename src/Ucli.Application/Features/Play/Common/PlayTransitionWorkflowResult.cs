using MackySoft.Ucli.Application.Features.Play.Common.Contracts;

namespace MackySoft.Ucli.Application.Features.Play.Common;

/// <summary>
/// Carries the direction-neutral result established by the Play Mode transition workflow.
/// </summary>
internal sealed record PlayTransitionWorkflowResult<TOutput>
    where TOutput : class
{
    private PlayTransitionWorkflowResult (
        TOutput? output,
        ApplicationFailure? error,
        PlayTransitionFailureContext? failureContext)
    {
        Output = output;
        Error = error;
        FailureContext = failureContext;
    }

    public TOutput? Output { get; }

    public ApplicationFailure? Error { get; }

    public PlayTransitionFailureContext? FailureContext { get; }

    public bool IsSuccess => Output is not null && Error is null;

    public static PlayTransitionWorkflowResult<TOutput> Success (
        TOutput output)
    {
        return new PlayTransitionWorkflowResult<TOutput>(
            output ?? throw new ArgumentNullException(nameof(output)),
            error: null,
            failureContext: null);
    }

    public static PlayTransitionWorkflowResult<TOutput> Failure (
        ApplicationFailure error,
        PlayTransitionFailureContext? failureContext = null)
    {
        return new PlayTransitionWorkflowResult<TOutput>(
            output: null,
            error ?? throw new ArgumentNullException(nameof(error)),
            failureContext);
    }
}
