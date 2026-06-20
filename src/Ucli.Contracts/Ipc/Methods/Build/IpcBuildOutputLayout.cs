namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents the command-derived BuildPipeline output layout for one <c>build.run</c> request. </summary>
/// <param name="Shape"> The BuildPipeline output shape literal. </param>
/// <param name="LocationPathName"> The absolute path passed to <c>BuildPlayerOptions.locationPathName</c>. </param>
public sealed record IpcBuildOutputLayout (
    string Shape,
    string LocationPathName);
