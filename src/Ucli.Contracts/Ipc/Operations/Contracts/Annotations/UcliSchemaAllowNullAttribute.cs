using System;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Allows generated JSON Schema for one property to include <c>null</c> in its type list. </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class UcliSchemaAllowNullAttribute : Attribute
{
}
