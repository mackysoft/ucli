namespace MackySoft.Ucli.Contracts;

/// <summary> Describes the static catalog metadata for one known uCLI error code. </summary>
/// <param name="Code"> The error code identifier. </param>
/// <param name="Category"> The stable category used by catalog filters. </param>
/// <param name="Summary"> A short summary of the failure condition. </param>
/// <param name="Meaning"> The static meaning of the code, independent from one concrete occurrence. </param>
/// <param name="AppliesTo"> The command identifiers that can emit this code. </param>
/// <param name="PossiblePhases"> The processing phases where this code can occur. </param>
/// <param name="ExecutionSemantics"> The default application-state and retry semantics for the code. </param>
/// <param name="Inspect"> The response fields or diagnostic commands callers should inspect. </param>
/// <param name="NextActions"> The follow-up actions associated with the code. </param>
/// <param name="RelatedCodes"> Other known error codes that represent adjacent conditions. </param>
public sealed record UcliErrorDescriptor
{
    /// <summary> Initializes one error-code descriptor. </summary>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="Code" /> is <see langword="null" />. </exception>
    public UcliErrorDescriptor (
        UcliCode Code,
        string Category,
        string Summary,
        string Meaning,
        IReadOnlyList<UcliCommand> AppliesTo,
        IReadOnlyList<string> PossiblePhases,
        UcliErrorExecutionSemantics ExecutionSemantics,
        IReadOnlyList<string> Inspect,
        IReadOnlyList<UcliErrorNextActionDescriptor> NextActions,
        IReadOnlyList<UcliCode> RelatedCodes)
    {
        this.Code = Code ?? throw new ArgumentNullException(nameof(Code));
        this.Category = Category;
        this.Summary = Summary;
        this.Meaning = Meaning;
        this.AppliesTo = AppliesTo;
        this.PossiblePhases = PossiblePhases;
        this.ExecutionSemantics = ExecutionSemantics;
        this.Inspect = Inspect;
        this.NextActions = NextActions;
        this.RelatedCodes = RelatedCodes;
    }

    public UcliCode Code { get; }

    public string Category { get; }

    public string Summary { get; }

    public string Meaning { get; }

    public IReadOnlyList<UcliCommand> AppliesTo { get; }

    public IReadOnlyList<string> PossiblePhases { get; }

    public UcliErrorExecutionSemantics ExecutionSemantics { get; }

    public IReadOnlyList<string> Inspect { get; }

    public IReadOnlyList<UcliErrorNextActionDescriptor> NextActions { get; }

    public IReadOnlyList<UcliCode> RelatedCodes { get; }
}
