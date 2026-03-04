using System;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace MackySoft.Ucli.Unity.Index
{
    /// <summary> Produces current index-input hashes from one Unity project directory. </summary>
    internal interface IIndexInputSnapshotProvider
    {
        /// <summary> Tries to compute one index-input hash snapshot for one project root path. </summary>
        /// <param name="projectRootPath"> The Unity project root path. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by operation pipelines. </param>
        /// <returns> The computed snapshot when successful; otherwise <see langword="null" />. </returns>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="projectRootPath" /> is <see langword="null" />, empty, or whitespace. </exception>
        ValueTask<IndexInputHashSnapshot?> TryCreate (
            string projectRootPath,
            CancellationToken cancellationToken = default);
    }
}
