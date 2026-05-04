using MackySoft.Ucli.Skills.Sources;

namespace MackySoft.Ucli.Skills.Hosts;

/// <summary> Provides deterministic materialization policy for one supported SKILL host. </summary>
public interface ISkillHostAdapter
{
    /// <summary> Gets the host descriptor. </summary>
    SkillHostDescriptor Descriptor { get; }

    /// <summary> Builds host-specific artifacts for one skill. </summary>
    /// <param name="metadata"> The host-independent source metadata. </param>
    /// <returns> The host-specific artifact set. </returns>
    SkillHostArtifactSet BuildArtifacts (SkillSourceMetadata metadata);
}
