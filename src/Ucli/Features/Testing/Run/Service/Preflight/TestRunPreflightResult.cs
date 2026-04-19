namespace MackySoft.Ucli.Features.Testing.Run.Service.Preflight;

/// <summary> Represents one preflight outcome for test-run execution. </summary>
/// <param name="Context"> The resolved execution context when preflight succeeds; otherwise <see langword="null" />. </param>
/// <param name="Failure"> The failure output when preflight fails; otherwise <see langword="null" />. </param>
internal sealed record TestRunPreflightResult (
    TestRunExecutionContext? Context,
    TestRunServiceResult? Failure)
{
    /// <summary> Gets a value indicating whether preflight succeeded. </summary>
    public bool IsSuccess => Context is not null && Failure is null;

    /// <summary> Creates one successful preflight result. </summary>
    /// <param name="context"> The resolved execution context. </param>
    /// <returns> The successful preflight result. </returns>
    public static TestRunPreflightResult Success (TestRunExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new TestRunPreflightResult(context, null);
    }

    /// <summary> Creates one failed preflight result. </summary>
    /// <param name="failure"> The failure output. </param>
    /// <returns> The failed preflight result. </returns>
    public static TestRunPreflightResult FailureResult (TestRunServiceResult failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new TestRunPreflightResult(null, failure);
    }
}