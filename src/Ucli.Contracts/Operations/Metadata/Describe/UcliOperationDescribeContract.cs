namespace MackySoft.Ucli.Contracts.Operations;

/// <summary> Describes the agent-facing contract for one primitive operation. </summary>
public sealed class UcliOperationDescribeContract
{
    /// <summary> Initializes a new instance of the <see cref="UcliOperationDescribeContract" /> class. </summary>
    public UcliOperationDescribeContract ()
    {
    }

    /// <summary> Initializes a new instance of the <see cref="UcliOperationDescribeContract" /> class. </summary>
    /// <param name="description"> The operation purpose description. </param>
    /// <param name="argsContract"> The generated contract for <c>steps[].args</c>. </param>
    /// <param name="resultContract">
    /// The generated contract for <c>opResults[].result</c>, or <see langword="null" /> when no result is emitted.
    /// </param>
    /// <param name="assurance"> The machine-readable assurance metadata. </param>
    /// <param name="codeContract"> The optional source-facing code contract. </param>
    public UcliOperationDescribeContract (
        string? description,
        UcliOperationJsonContract? argsContract,
        UcliOperationJsonContract? resultContract,
        UcliOperationAssuranceContract? assurance,
        UcliOperationCodeContract? codeContract)
    {
        Description = description;
        ArgsContract = argsContract;
        ResultContract = resultContract;
        Assurance = assurance;
        CodeContract = codeContract;
    }

    /// <summary> Gets or sets the operation purpose description. </summary>
    public string? Description { get; set; }

    /// <summary> Gets or sets the generated contract for <c>steps[].args</c>. </summary>
    public UcliOperationJsonContract? ArgsContract { get; set; }

    /// <summary>
    /// Gets or sets the generated contract for <c>opResults[].result</c>, or <see langword="null" /> when no result is emitted.
    /// </summary>
    public UcliOperationJsonContract? ResultContract { get; set; }

    /// <summary> Gets or sets the machine-readable assurance metadata. </summary>
    public UcliOperationAssuranceContract? Assurance { get; set; }

    /// <summary> Gets or sets the optional source-facing code contract. </summary>
    public UcliOperationCodeContract? CodeContract { get; set; }
}
