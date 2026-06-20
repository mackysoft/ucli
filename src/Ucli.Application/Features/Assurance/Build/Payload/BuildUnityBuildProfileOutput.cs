namespace MackySoft.Ucli.Application.Features.Assurance.Build.Payload;

/// <summary> Represents the Unity Build Profile asset resolved for a build run. </summary>
internal sealed record BuildUnityBuildProfileOutput (
    string Path,
    string Digest);
