using System;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Excludes one property from generated JSON Schema. </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class UcliSchemaIgnoreAttribute : Attribute
{
}
