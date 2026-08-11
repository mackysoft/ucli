using System.Text.Json.Serialization;
using MackySoft.JsonSchema.Generation.Annotations;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Presentation;

/// <summary>
/// Represents the positive width and height of a pixel surface as a structurally comparable value.
/// </summary>
[Description("The positive width and height of a pixel surface.")]
public sealed record PixelDimensions
{
    /// <summary> Initializes pixel dimensions. </summary>
    /// <param name="width"> The positive surface width in pixels. </param>
    /// <param name="height"> The positive surface height in pixels. </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="width" /> or <paramref name="height" /> is less than or equal to zero.
    /// </exception>
    [JsonConstructor]
    public PixelDimensions (int width, int height)
    {
        Width = ContractArgumentGuard.RequirePositive(width, nameof(width));
        Height = ContractArgumentGuard.RequirePositive(height, nameof(height));
    }

    /// <summary>Gets the surface width in pixels.</summary>
    [JsonInclude]
    [JsonRequired]
    [UcliInt32Minimum(1)]
    [Description("The positive surface width in pixels.")]
    public int Width { get; private init; }

    /// <summary>Gets the surface height in pixels.</summary>
    [JsonInclude]
    [JsonRequired]
    [UcliInt32Minimum(1)]
    [Description("The positive surface height in pixels.")]
    public int Height { get; private init; }
}
