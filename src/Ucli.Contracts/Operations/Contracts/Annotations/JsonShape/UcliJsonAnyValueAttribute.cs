namespace MackySoft.Ucli.Contracts.Operations;

/// <summary> Marks one contract property as accepting any JSON value. </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class UcliJsonAnyValueAttribute : Attribute
{
}
