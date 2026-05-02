using System;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Includes <c>null</c> in the generated JSON Schema type list for one property. </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class UcliNullableAttribute : Attribute
{
}
