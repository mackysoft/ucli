using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Features.Requests.Resolve.UseCases.Resolve;

/// <summary> Represents selector creation outcome for <c>ucli resolve</c>. </summary>
internal sealed record ResolveSelectorInputCreationResult (
    ResolveSelectorInput? Selector,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether selector creation succeeded. </summary>
    public bool IsSuccess => Selector is not null && Error is null;

    /// <summary> Creates a successful selector creation result. </summary>
    public static ResolveSelectorInputCreationResult Success (ResolveSelectorInput selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        return new ResolveSelectorInputCreationResult(selector, null);
    }

    /// <summary> Creates a failed selector creation result. </summary>
    public static ResolveSelectorInputCreationResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new ResolveSelectorInputCreationResult(null, error);
    }
}