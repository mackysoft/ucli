namespace MackySoft.Ucli.Application.Shared.Execution;

/// <summary>
/// Represents a completed application result that owns the verdict projected by its public payload.
/// </summary>
internal interface IVerdictResult
{
    /// <summary> Gets the verdict established by the completed result. </summary>
    Verdict Verdict { get; }
}
