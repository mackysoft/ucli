using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents monotonic Unity lifecycle generation values captured at one point in time. </summary>
/// <param name="CompileGeneration"> The monotonic compile generation. </param>
/// <param name="DomainReloadGeneration"> The monotonic domain-reload generation. </param>
/// <param name="AssetRefreshGeneration"> The monotonic asset-refresh generation. </param>
/// <param name="PlayModeGeneration"> The monotonic Play Mode generation. </param>
public sealed record IpcUnityGenerationSnapshot
{
    /// <summary> Initializes a generation snapshot from four observed counters. </summary>
    [JsonConstructor]
    public IpcUnityGenerationSnapshot (
        long CompileGeneration,
        long DomainReloadGeneration,
        long AssetRefreshGeneration,
        long PlayModeGeneration)
    {
        EnsureNonNegative(CompileGeneration, nameof(CompileGeneration));
        EnsureNonNegative(DomainReloadGeneration, nameof(DomainReloadGeneration));
        EnsureNonNegative(AssetRefreshGeneration, nameof(AssetRefreshGeneration));
        EnsureNonNegative(PlayModeGeneration, nameof(PlayModeGeneration));

        this.CompileGeneration = CompileGeneration;
        this.DomainReloadGeneration = DomainReloadGeneration;
        this.AssetRefreshGeneration = AssetRefreshGeneration;
        this.PlayModeGeneration = PlayModeGeneration;
    }

    /// <summary> Gets the compile generation. </summary>
    [JsonInclude]
    [JsonRequired]
    public long CompileGeneration { get; private init; }

    /// <summary> Gets the domain-reload generation. </summary>
    [JsonInclude]
    [JsonRequired]
    public long DomainReloadGeneration { get; private init; }

    /// <summary> Gets the asset-refresh generation. </summary>
    [JsonInclude]
    [JsonRequired]
    public long AssetRefreshGeneration { get; private init; }

    /// <summary> Gets the Play Mode generation. </summary>
    [JsonInclude]
    [JsonRequired]
    public long PlayModeGeneration { get; private init; }

    private static void EnsureNonNegative (long value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Generation must be non-negative.");
        }
    }
}
