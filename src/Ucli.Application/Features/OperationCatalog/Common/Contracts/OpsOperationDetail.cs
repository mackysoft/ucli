using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Application.Features.OperationCatalog.Common.Contracts;

/// <summary> Represents one detailed operation payload entry. </summary>
internal sealed class OpsOperationDetail
{
    /// <summary> Initializes a new instance of the <see cref="OpsOperationDetail" /> class. </summary>
    public OpsOperationDetail (
        string name,
        UcliOperationKind kind,
        OperationPolicy policy,
        UcliOperationPlayModeSupport playModeSupport,
        string description,
        UcliOperationJsonContract argsContract,
        UcliOperationJsonContract? resultContract,
        UcliOperationAssuranceContract assurance,
        UcliOperationCodeContract? codeContract)
    {
        Name = name;
        Kind = kind;
        Policy = policy;
        PlayModeSupport = playModeSupport;
        Description = description;
        ArgsContract = argsContract;
        ResultContract = resultContract;
        Assurance = assurance;
        CodeContract = codeContract;
    }

    /// <summary> Gets the operation name. </summary>
    public string Name { get; }

    /// <summary> Gets the operation kind. </summary>
    public UcliOperationKind Kind { get; }

    /// <summary> Gets the operation policy. </summary>
    public OperationPolicy Policy { get; }

    /// <summary> Gets the Play Mode support for public raw operation execution. </summary>
    public UcliOperationPlayModeSupport PlayModeSupport { get; }

    /// <summary> Gets the operation purpose description. </summary>
    public string Description { get; }

    /// <summary> Gets the generated operation argument contract. </summary>
    public UcliOperationJsonContract ArgsContract { get; }

    /// <summary> Gets the generated operation result contract, or <see langword="null" /> when no result is emitted. </summary>
    public UcliOperationJsonContract? ResultContract { get; }

    /// <summary> Gets machine-readable assurance metadata. </summary>
    public UcliOperationAssuranceContract Assurance { get; }

    /// <summary> Gets optional source-facing code metadata. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UcliOperationCodeContract? CodeContract { get; }

}
