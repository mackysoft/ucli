namespace MackySoft.Ucli.Contracts.Text;

/// <summary> Defines the canonical contract literal for one enum member. </summary>
/// <remarks>
/// Define at least one enum member and apply this attribute to every declared member of an external enum consumed by
/// <see cref="ContractLiteralCodec" /> or <see cref="ContractLiteralJsonConverterFactory" />.
/// </remarks>
[AttributeUsage(AttributeTargets.Field)]
public sealed class UcliContractLiteralAttribute : Attribute
{
    /// <summary> Initializes a new instance of the <see cref="UcliContractLiteralAttribute" /> class. </summary>
    /// <param name="literal"> The canonical contract literal. </param>
    public UcliContractLiteralAttribute (string literal)
    {
        Literal = literal;
    }

    /// <summary> Gets the canonical contract literal. </summary>
    public string Literal { get; }
}
