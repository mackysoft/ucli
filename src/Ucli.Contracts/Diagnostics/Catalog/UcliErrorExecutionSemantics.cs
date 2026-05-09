namespace MackySoft.Ucli.Contracts;

/// <summary> Describes the default execution-state meaning of one error code. </summary>
/// <param name="ImpliesNotApplied"> <see langword="true" /> when the code alone proves no operation was applied, <see langword="false" /> when it does not, or <see langword="null" /> when the code alone cannot decide. </param>
/// <param name="MayBeIndeterminate"> Whether the code can leave the request application state unknown. </param>
/// <param name="SafeToRetry"> One value from <see cref="UcliErrorRetryClassValues" /> that classifies default retry safety. </param>
public sealed record UcliErrorExecutionSemantics (
    bool? ImpliesNotApplied,
    bool MayBeIndeterminate,
    string SafeToRetry);
