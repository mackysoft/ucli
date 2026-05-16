namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a projected item list and its bounded window metadata. </summary>
internal sealed record BoundedWindowResult<T> (
    IReadOnlyList<T> Items,
    BoundedWindow Window);
