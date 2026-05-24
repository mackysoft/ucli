namespace MackySoft.Ucli.Contracts.Operations;

/// <summary> Describes how to interpret <c>opResults[].result</c> for one operation. </summary>
public sealed class UcliOperationResultContract
{
    /// <summary> Initializes a new instance of the <see cref="UcliOperationResultContract" /> class. </summary>
    public UcliOperationResultContract ()
    {
    }

    /// <summary> Initializes a new instance of the <see cref="UcliOperationResultContract" /> class. </summary>
    public UcliOperationResultContract (
        bool emitted,
        string? resultType,
        string? description)
    {
        Emitted = emitted;
        ResultType = resultType;
        Description = description;
    }

    /// <summary> Creates a contract for an operation that emits no result payload. </summary>
    /// <param name="description"> The result contract description. </param>
    /// <returns> The no-result contract. </returns>
    public static UcliOperationResultContract NoResult (string description)
    {
        return new UcliOperationResultContract(
            emitted: false,
            resultType: nameof(UcliNoResult),
            description: description);
    }

    /// <summary> Creates a contract for an operation that emits one typed result payload. </summary>
    /// <typeparam name="TResult"> The operation result contract type. </typeparam>
    /// <param name="description"> The result contract description. </param>
    /// <returns> The typed result contract. </returns>
    public static UcliOperationResultContract One<TResult> (string description)
    {
        return new UcliOperationResultContract(
            emitted: true,
            resultType: typeof(TResult).Name,
            description: description);
    }

    /// <summary> Gets or sets a value indicating whether <c>opResults[].result</c> is emitted. </summary>
    public bool Emitted { get; set; }

    /// <summary> Gets or sets the result contract type name. </summary>
    public string? ResultType { get; set; }

    /// <summary> Gets or sets the result meaning and reading guidance. </summary>
    public string? Description { get; set; }
}
