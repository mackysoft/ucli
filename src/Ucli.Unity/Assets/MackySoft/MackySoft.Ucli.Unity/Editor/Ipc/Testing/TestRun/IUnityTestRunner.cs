using System.Threading;
using System.Threading.Tasks;
using UnityEditor.TestTools.TestRunner.Api;

#nullable enable annotations

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Executes Unity Test Framework runs from normalized test-run request contexts. </summary>
    internal interface IUnityTestRunner
    {
        /// <summary> Executes one Unity Test Framework run and returns the result adaptor. </summary>
        /// <param name="requestContext"> The normalized test-run request context. </param>
        /// <param name="progressSink"> The optional sink that receives live test progress entries. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by caller. </param>
        /// <returns> The completed test result adaptor. </returns>
        Task<ITestResultAdaptor> RunAsync (
            UnityTestRunRequestContext requestContext,
            IUnityTestRunProgressSink? progressSink = null,
            CancellationToken cancellationToken = default);
    }
}
