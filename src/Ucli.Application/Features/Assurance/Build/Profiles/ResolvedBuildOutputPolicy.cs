namespace MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;

/// <summary> Represents the build output policy owned by the build run command contract. </summary>
internal sealed record ResolvedBuildOutputPolicy (
    BuildProfileOutputKind Kind);
