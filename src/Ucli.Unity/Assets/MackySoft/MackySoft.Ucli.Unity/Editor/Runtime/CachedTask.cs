using System.Threading.Tasks;

namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary> Provides cached completed tasks for frequently returned synchronous results. </summary>
    internal static class CachedTask
    {
        private static readonly Task<bool> TrueTask = Task.FromResult(true);

        private static readonly Task<bool> FalseTask = Task.FromResult(false);

        /// <summary> Returns a cached completed boolean task for <paramref name="result" />. </summary>
        /// <param name="result"> The boolean result value to represent. </param>
        /// <returns> A cached completed task for <paramref name="result" />. </returns>
        public static Task<bool> FromResult (bool result)
        {
            return result ? TrueTask : FalseTask;
        }
    }
}
