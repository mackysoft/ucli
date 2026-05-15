namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents normalized bounded result windowing options. </summary>
internal sealed record BoundedWindowOptions (
    bool All,
    int Limit,
    string? Cursor,
    int Offset);
