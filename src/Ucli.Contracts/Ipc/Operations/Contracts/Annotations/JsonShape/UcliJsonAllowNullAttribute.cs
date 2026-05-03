using System;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Allows one contract property to accept explicit JSON <c>null</c>. </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class UcliJsonAllowNullAttribute : Attribute
{
}
