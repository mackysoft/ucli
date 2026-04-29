namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Represents one type entry contract in <c>types.catalog.json</c>. </summary>
/// <param name="TypeId"> The stable type identifier value. </param>
/// <param name="DisplayName"> The display-name value. </param>
/// <param name="Namespace"> The type namespace value. </param>
/// <param name="AssemblyName"> The assembly-name value. </param>
/// <param name="BaseTypeId"> The base-type identifier value. </param>
/// <param name="Flags"> The type flags contract. </param>
internal sealed record IndexTypeEntryJsonContract (
    string? TypeId,
    string? DisplayName,
    string? Namespace,
    string? AssemblyName,
    string? BaseTypeId,
    IndexTypeFlagsJsonContract? Flags);
