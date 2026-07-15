using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Unity.Runtime;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityRuntimeServiceCollectionExtensionsTests
    {
        private static readonly TimeSpan AsyncWaitTimeout = TimeSpan.FromSeconds(5);

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator AddUnityRuntimeServices_WhenExecutorsAreResolvedOffMainThread_PreservesRegistrationThreadBinding () => UniTask.ToCoroutine(async () =>
        {
            var mainThreadId = Thread.CurrentThread.ManagedThreadId;
            var services = new ServiceCollection();
            services.AddUnityRuntimeServices(DaemonEditorMode.Gui);
            using var serviceProvider = services.BuildServiceProvider();

            var executors = await Task.Run(() =>
            {
                return (
                    Control: serviceProvider.GetRequiredService<IUnityControlPlaneRequestExecutor>(),
                    ControlLifetime: serviceProvider.GetRequiredService<IUnityControlPlaneRequestLifetime>(),
                    Mutation: serviceProvider.GetRequiredService<IUnityMainThreadRequestExecutor>());
            });

            Assert.That(executors.ControlLifetime, Is.SameAs(executors.Control));

            var controlThreadId = await TestAwaiter.WaitAsync(
                executors.Control.ExecuteAsync(
                        () => Task.FromResult(Thread.CurrentThread.ManagedThreadId),
                        CancellationToken.None)
                    .AsUniTask(),
                "Control executor resolved off the Unity main thread",
                AsyncWaitTimeout);
            var mutationThreadId = await TestAwaiter.WaitAsync(
                executors.Mutation.ExecuteAsync(
                        () => Task.FromResult(Thread.CurrentThread.ManagedThreadId),
                        CancellationToken.None)
                    .AsUniTask(),
                "Mutation executor resolved off the Unity main thread",
                AsyncWaitTimeout);

            Assert.That(controlThreadId, Is.EqualTo(mainThreadId));
            Assert.That(mutationThreadId, Is.EqualTo(mainThreadId));
        });
    }
}
