using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Features.OperationCatalog.Catalog.Access;

namespace MackySoft.Ucli.Features.OperationCatalog.UseCases.Ops.Projection;

/// <summary> Maps internal access metadata into command-facing <c>payload.readIndex</c> output. </summary>
internal sealed class OpsReadIndexInfoMapper
{
    /// <summary> Maps one internal access-info value into command-facing read-index output. </summary>
    /// <param name="accessInfo"> The internal access metadata. </param>
    /// <returns> The mapped read-index output. </returns>
    public ReadIndexInfo Map (OpsCatalogAccessInfo accessInfo)
    {
        ArgumentNullException.ThrowIfNull(accessInfo);

        return new ReadIndexInfo(
            Used: accessInfo.Used,
            Hit: accessInfo.Hit,
            Source: MapSource(accessInfo.Source),
            Freshness: ReadIndexInfoTextCodec.MapFreshness(accessInfo.Freshness),
            GeneratedAtUtc: accessInfo.GeneratedAtUtc,
            FallbackReason: accessInfo.FallbackReason);
    }

    private static string MapSource (OpsCatalogSource source)
    {
        return source switch
        {
            OpsCatalogSource.Index => ReadIndexInfoTextCodec.SourceIndex,
            OpsCatalogSource.Source => ReadIndexInfoTextCodec.SourceUnity,
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, "Unsupported ops catalog source."),
        };
    }
}