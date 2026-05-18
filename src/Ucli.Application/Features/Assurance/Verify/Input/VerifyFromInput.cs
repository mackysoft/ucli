namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Input;

/// <summary> Represents normalized data read from <c>verify --from</c> input. </summary>
internal sealed record VerifyFromInput (
    string Command,
    string ProjectFingerprint,
    IReadOnlyList<VerifyFromOperationResult> OpResults,
    int ReadPostconditionRequirementCount)
{
    /// <summary> Gets a value indicating whether the input contains data that needs post-read claims. </summary>
    public bool NeedsPostRead => ReadPostconditionRequirementCount > 0
        || OpResults.Any(static result => result.Applied || result.Changed || result.TouchedCount > 0 || result.Diagnostics.Count > 0);
}
