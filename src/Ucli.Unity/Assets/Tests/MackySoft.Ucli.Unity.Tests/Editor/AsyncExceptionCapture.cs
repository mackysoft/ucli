using System;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Tests
{
    internal static class ExceptionCapture
    {

        public static async UniTask<TException> Capture<TException> (Func<UniTask> action)
            where TException : Exception
        {
            try
            {
                await action();
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
