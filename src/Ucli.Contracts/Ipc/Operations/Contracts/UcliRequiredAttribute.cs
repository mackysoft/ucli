using System;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Marks one operation contract property as required in generated JSON Schema. </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class UcliRequiredAttribute : Attribute
{
}
