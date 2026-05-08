using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Describes code-facing API contracts for operations that compile or execute user code. </summary>
public sealed class UcliOperationCodeContract
{
    /// <summary> Initializes a new instance of the <see cref="UcliOperationCodeContract" /> class. </summary>
    public UcliOperationCodeContract ()
    {
    }

    /// <summary> Initializes a new instance of the <see cref="UcliOperationCodeContract" /> class. </summary>
    /// <param name="language"> The source language literal. </param>
    /// <param name="entryPoint"> The entry point contract. </param>
    /// <param name="sourceForms"> The accepted source forms. </param>
    /// <param name="apiTypes"> The source-visible API type contracts. </param>
    public UcliOperationCodeContract (
        string? language,
        UcliCodeEntryPointContract? entryPoint,
        IReadOnlyList<UcliCodeSourceFormContract>? sourceForms,
        IReadOnlyList<UcliCodeApiTypeContract>? apiTypes)
    {
        Language = language;
        EntryPoint = entryPoint;
        SourceForms = sourceForms;
        ApiTypes = apiTypes;
    }

    /// <summary> Gets or sets the source language literal. </summary>
    public string? Language { get; set; }

    /// <summary> Gets or sets the entry point contract. </summary>
    public UcliCodeEntryPointContract? EntryPoint { get; set; }

    /// <summary> Gets or sets the accepted source forms. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<UcliCodeSourceFormContract>? SourceForms { get; set; }

    /// <summary> Gets or sets the source-visible API type contracts. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<UcliCodeApiTypeContract>? ApiTypes { get; set; }
}
