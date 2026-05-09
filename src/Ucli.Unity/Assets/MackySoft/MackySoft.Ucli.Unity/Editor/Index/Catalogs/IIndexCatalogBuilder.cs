using System;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace MackySoft.Ucli.Unity.Index
{
    /// <summary> Builds index catalog contracts from the current Unity project state. </summary>
    internal interface IIndexCatalogBuilder
    {
        /// <summary> Builds one full index catalog snapshot for one project root path. </summary>
        /// <param name="projectRootPath"> The Unity project root path. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by operation pipelines. </param>
        /// <returns> The build result. </returns>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="projectRootPath" /> is <see langword="null" />, empty, or whitespace. </exception>
        ValueTask<IndexCatalogBuildResult> BuildAsync (
            string projectRootPath,
            CancellationToken cancellationToken = default);
    }
}