#nullable enable

namespace MackySoft.Ucli.Unity.Index
{
    /// <summary> Resolves project type sets used to build index catalogs. </summary>
    internal interface IIndexProjectTypeCatalogSource
    {
        /// <summary> Resolves component/asset/serialize-reference type sets from current project state. </summary>
        /// <returns> The resolved project type catalog. </returns>
        IndexProjectTypeCatalog Resolve ();
    }
}