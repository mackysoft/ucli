using System;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Marks one operation contract property as required in the generated structural schema. </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class UcliRequiredAttribute : Attribute
{
}
