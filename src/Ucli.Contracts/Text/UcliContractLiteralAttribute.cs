namespace MackySoft.Ucli.Contracts.Text;

/// <summary> Defines the canonical contract literal for one enum member. </summary>
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
