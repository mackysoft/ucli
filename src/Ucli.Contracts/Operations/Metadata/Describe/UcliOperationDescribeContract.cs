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
    /// <param name="inputs"> The input contracts used to build <c>steps[].args</c>. </param>
    /// <param name="resultContract"> The contract for interpreting <c>opResults[].result</c>. </param>
    /// <param name="assurance"> The machine-readable assurance metadata. </param>
    /// <param name="codeContract"> The optional source-facing code contract. </param>
    public UcliOperationDescribeContract (
        string? description,
        IReadOnlyList<UcliOperationInputContract>? inputs,
        UcliOperationResultContract? resultContract,
        UcliOperationAssuranceContract? assurance,
        UcliOperationCodeContract? codeContract)
    {
        Description = description;
        Inputs = inputs;
        ResultContract = resultContract;
        Assurance = assurance;
        CodeContract = codeContract;
    }

    /// <summary> Gets or sets the operation purpose description. </summary>
    public string? Description { get; set; }

    /// <summary> Gets or sets input contracts used to build <c>steps[].args</c>. </summary>
    public IReadOnlyList<UcliOperationInputContract>? Inputs { get; set; }

    /// <summary> Gets or sets the contract for interpreting <c>opResults[].result</c>. </summary>
    public UcliOperationResultContract? ResultContract { get; set; }

    /// <summary> Gets or sets the machine-readable assurance metadata. </summary>
    public UcliOperationAssuranceContract? Assurance { get; set; }

    /// <summary> Gets or sets the optional source-facing code contract. </summary>
    public UcliOperationCodeContract? CodeContract { get; set; }
}
