namespace MackySoft.Ucli.Contracts.Ipc.ContractReading;

/// <summary> Represents normalized <c>expect</c> constraint values. </summary>
/// <param name="NonNull"> The optional non-null constraint flag. </param>
/// <param name="Count"> The optional exact-count constraint. </param>
/// <param name="Min"> The optional minimum-count constraint. </param>
/// <param name="Max"> The optional maximum-count constraint. </param>
internal readonly record struct ExpectationConstraints (
    bool? NonNull,
    int? Count,
    int? Min,
    int? Max);
