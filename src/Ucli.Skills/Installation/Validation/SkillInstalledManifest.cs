using MackySoft.Ucli.Skills.Manifests;

namespace MackySoft.Ucli.Skills.Installation.Validation;

/// <summary> Represents a validated installed SKILL manifest and its source text. </summary>
/// <param name="ManifestPath"> The installed manifest file path. </param>
/// <param name="ManifestText"> The installed manifest JSON text. </param>
/// <param name="Manifest"> The validated manifest model. </param>
public sealed record SkillInstalledManifest (
    string ManifestPath,
    string ManifestText,
    SkillManifest Manifest);
