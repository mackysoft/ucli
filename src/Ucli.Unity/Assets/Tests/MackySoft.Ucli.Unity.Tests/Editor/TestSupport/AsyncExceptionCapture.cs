using System;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Tests
{
    /// <summary>
    /// Captures an expected asynchronous exception with an explicit timeout fuse.
    /// </summary>
    internal static class AsyncExceptionCapture
    {
        public static async UniTask<TException> CaptureAsync<TException> (
            Func<UniTask> action,
            string description,
            TimeSpan timeout)
            where TException : Exception
        {
            try
            {
                await TestAwaiter.WaitAsync(action(), description, timeout);
            }
            catch (TException exception)
            {
                return exception;
            }

            Assert.Fail($"{typeof(TException).Name} was expected.");
            return null;
        }
    }
}