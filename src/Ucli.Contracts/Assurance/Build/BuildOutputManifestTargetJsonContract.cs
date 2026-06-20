namespace MackySoft.Ucli.Contracts.Assurance.Build;

/// <summary> Represents the resolved build target identity recorded in <c>output-manifest.json</c>. </summary>
/// <param name="StableName"> The uCLI build target stable name. </param>
/// <param name="UnityBuildTarget"> The Unity <c>BuildTarget</c> enum member name. </param>
internal sealed record BuildOutputManifestTargetJsonContract (
    string StableName,
    string UnityBuildTarget);
