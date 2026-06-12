namespace MackySoft.Ucli.Application.Features.Assurance.Build.Payload;

/// <summary> Represents resolved build scene input. </summary>
internal sealed record BuildScenesOutput (
    string Source,
    IReadOnlyList<string> Paths);
