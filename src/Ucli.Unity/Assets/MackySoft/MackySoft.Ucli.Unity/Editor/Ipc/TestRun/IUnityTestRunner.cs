using System.Threading;
using System.Threading.Tasks;
using UnityEditor.TestTools.TestRunner.Api;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Executes Unity Test Framework runs from normalized test-run request contexts. </summary>
    internal interface IUnityTestRunner
    {
        /// <summary> Executes one Unity Test Framework run and returns the result adaptor. </summary>
        /// <param name="requestContext"> The normalized test-run request context. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by caller. </param>
        /// <returns> The completed test result adaptor. </returns>
        Task<ITestResultAdaptor> Run (
            UnityTestRunRequestContext requestContext,
            CancellationToken cancellationToken = default);
    }
}
