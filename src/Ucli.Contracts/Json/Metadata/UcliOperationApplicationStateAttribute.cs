namespace MackySoft.Ucli.Contracts.Json.Metadata;

/// <summary>
/// Declares the four-state operation application contract that excludes the
/// lifecycle-only partial-application state.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class UcliOperationApplicationStateAttribute : Attribute;
