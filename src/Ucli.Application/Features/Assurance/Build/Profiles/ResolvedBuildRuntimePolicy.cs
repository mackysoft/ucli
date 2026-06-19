namespace MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;

/// <summary> Represents runtime policy resolved from a build profile. </summary>
internal sealed record ResolvedBuildRuntimePolicy (
    IReadOnlyList<BuildProfileRuntimeExecutionMode> AllowedExecutionModes,
    IReadOnlyList<DaemonEditorMode> AllowedEditorModes);
