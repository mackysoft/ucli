using System;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Marks one property as accepting any JSON value in generated JSON Schema. </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class UcliSchemaAnyAttribute : Attribute
{
}
