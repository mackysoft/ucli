using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;

/// <summary> Represents one resolved build profile consumed by build execution. </summary>
internal sealed class ResolvedBuildProfile
{
    /// <summary> Gets the only build profile schema version supported by this model. </summary>
    public const int SupportedSchemaVersion = 1;

    /// <summary> Initializes a resolved build profile. </summary>
    public ResolvedBuildProfile (
        int schemaVersion,
        ResolvedBuildInputs inputs,
        ResolvedBuildRunner runner,
        ResolvedBuildPolicy policy)
    {
        if (schemaVersion != SupportedSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(
                nameof(schemaVersion),
                schemaVersion,
                $"Build profile schema version must be {SupportedSchemaVersion}.");
        }

        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(policy);
        if (inputs is ResolvedBuildInputs.UnityBuildProfile
            && runner is not ResolvedBuildRunner.BuildPipeline)
        {
            throw new ArgumentException(
                "Unity Build Profile inputs require a BuildPipeline runner.",
                nameof(runner));
        }

        SchemaVersion = schemaVersion;
        Inputs = inputs;
        Runner = runner;
        Policy = policy;
        Digest = BuildProfileDigestCalculator.Calculate(schemaVersion, inputs, runner, policy);
    }

    /// <summary> Gets the build profile schema version. </summary>
    public int SchemaVersion { get; }

    /// <summary> Gets the resolved build inputs. </summary>
    public ResolvedBuildInputs Inputs { get; }

    /// <summary> Gets the resolved build runner. </summary>
    public ResolvedBuildRunner Runner { get; }

    /// <summary> Gets the resolved build policy. </summary>
    public ResolvedBuildPolicy Policy { get; }

    /// <summary> Gets the canonical build profile digest. </summary>
    public Sha256Digest Digest { get; }
}
