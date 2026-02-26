namespace MackySoft.Ucli.Configuration;

/// <summary> Represents parsed values from <c>.ucli/config.json</c>. </summary>
/// <param name="SchemaVersion"> The config schema version. </param>
/// <param name="OperationPolicy"> The allowed operation safety level. </param>
/// <param name="PlanTokenMode"> The plan token requirement level. </param>
/// <param name="OperationAllowlist"> The operation-name allowlist patterns. </param>
internal sealed record UcliConfig (
    int SchemaVersion,
    OperationPolicy OperationPolicy,
    PlanTokenMode PlanTokenMode,
    IReadOnlyList<string> OperationAllowlist)
{
    private const int CurrentSchemaVersion = 1;
    private const string DefaultAllowlistPattern = "^ucli\\.";

    /// <summary> Creates default configuration values for missing config files. </summary>
    /// <returns> The default config instance. </returns>
    public static UcliConfig CreateDefault ()
    {
        return new UcliConfig(
            SchemaVersion: CurrentSchemaVersion,
            OperationPolicy: OperationPolicy.Safe,
            PlanTokenMode: PlanTokenMode.Optional,
            OperationAllowlist:
            [
                DefaultAllowlistPattern,
            ]);
    }
}