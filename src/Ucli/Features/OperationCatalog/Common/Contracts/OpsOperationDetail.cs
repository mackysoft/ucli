using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Features.OperationCatalog.Common.Contracts;

/// <summary> Represents one detailed operation payload entry. </summary>
internal sealed class OpsOperationDetail
{
    /// <summary> Initializes a new instance of the <see cref="OpsOperationDetail" /> class. </summary>
    public OpsOperationDetail (
        string name,
        string kind,
        string policy,
        string description,
        IReadOnlyList<UcliOperationInputContract> inputs,
        UcliOperationResultContract resultContract,
        UcliOperationAssuranceContract assurance,
        JsonElement argsSchema,
        JsonElement? resultSchema)
    {
        Name = name;
        Kind = kind;
        Policy = policy;
        Description = description;
        Inputs = inputs;
        ResultContract = resultContract;
        Assurance = assurance;
        ArgsSchema = argsSchema;
        ResultSchema = resultSchema;
    }

    /// <summary> Gets the operation name. </summary>
    public string Name { get; }

    /// <summary> Gets the operation kind literal. </summary>
    public string Kind { get; }

    /// <summary> Gets the operation policy literal. </summary>
    public string Policy { get; }

    /// <summary> Gets the operation purpose description. </summary>
    public string Description { get; }

    /// <summary> Gets input contracts used to build <c>steps[].args</c>. </summary>
    public IReadOnlyList<UcliOperationInputContract> Inputs { get; }

    /// <summary> Gets the contract for interpreting <c>opResults[].result</c>. </summary>
    public UcliOperationResultContract ResultContract { get; }

    /// <summary> Gets machine-readable assurance metadata. </summary>
    public UcliOperationAssuranceContract Assurance { get; }

    /// <summary> Gets the JSON schema object for operation arguments. </summary>
    public JsonElement ArgsSchema { get; }

    /// <summary> Gets the JSON schema object for operation result, or <see langword="null" /> when no result is emitted. </summary>
    public JsonElement? ResultSchema { get; }
}
