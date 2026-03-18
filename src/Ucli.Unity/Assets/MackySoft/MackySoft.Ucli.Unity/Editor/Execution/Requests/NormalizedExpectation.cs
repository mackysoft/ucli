namespace MackySoft.Ucli.Unity.Execution.Requests
{
    /// <summary> Represents normalized shared expectation constraints for one operation output. </summary>
    /// <param name="NonNull"> The optional non-null constraint flag. </param>
    /// <param name="Count"> The optional exact-count constraint. </param>
    /// <param name="Min"> The optional minimum-count constraint. </param>
    /// <param name="Max"> The optional maximum-count constraint. </param>
    public sealed record NormalizedExpectation (
        bool? NonNull,
        int? Count,
        int? Min,
        int? Max);
}