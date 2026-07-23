using System;
using System.IO;
using System.Threading;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Authorization;
using MackySoft.Ucli.Infrastructure.Project;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Unity.Ipc;
using MackySoft.Ucli.Unity.Project;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityIpcServiceCollectionExtensionsTests
    {
        private static readonly DateTimeOffset ObservedUtc =
            new DateTimeOffset(2026, 7, 15, 0, 0, 0, TimeSpan.Zero);

        [Test]
        [Category("Size.Small")]
        public void AddDaemonHostServices_ResolvesConnectionHandlerServerAndRecoverableStore ()
        {
            var services = CreateApplicationServices(
                out var projectPath,
                out var projectFingerprint);
            var endpoint = CreateEndpoint("daemon");
            var bootstrapContext = new UnityDaemonBootstrapContext(
                AbsolutePath.Parse(projectPath),
                projectFingerprint,
                AbsolutePath.Parse(Path.Combine(
                    projectPath,
                    "Library",
                    "ucli-composition-root-session.json")),
                Guid.NewGuid(),
                ObservedUtc,
                UnityIpcEndpointBinding.Create(endpoint));
            services.AddUnityIpcDaemonHostServices(
                bootstrapContext,
                new DaemonLogRingBuffer(),
                Guid.NewGuid());

            using var serviceProvider = services.BuildServiceProvider();

            Assert.That(
                serviceProvider.GetRequiredService<IUnityIpcConnectionHandler>(),
                Is.TypeOf<UnityIpcConnectionHandler>());
            Assert.That(
                serviceProvider.GetRequiredService<IUnityIpcServer>(),
                Is.TypeOf<UnityIpcServer>());
            Assert.That(
                serviceProvider.GetRequiredService<IRecoverableIpcOperationStore>(),
                Is.TypeOf<FileRecoverableIpcOperationStore>());
        }

        [Test]
        [Category("Size.Small")]
        public void AddOneshotHostServices_ResolvesSuppliedWatchdogAndHostWithoutRecoverableStore ()
        {
            var services = CreateApplicationServices(out _, out var projectFingerprint);
            var endpoint = CreateEndpoint("oneshot");
            var exitCount = 0;
            var watchdog = new OneshotProcessLifetimeWatchdog(
                storageRoot: AbsolutePath.Parse(
                    Path.Combine(Path.GetTempPath(), "ucli-composition-root-tests")),
                bootstrapEnvelope: new IpcOneshotBootstrapEnvelope(
                    BootstrapId: Guid.NewGuid(),
                    ParentProcess: new ProcessIdentity(42, 123),
                    ProjectFingerprint: projectFingerprint,
                    SessionToken: IpcSessionToken.CreateRandom(),
                    CreatedAtUtc: ObservedUtc,
                    ExitDeadlineUtc: ObservedUtc.AddMinutes(1),
                    Endpoint: endpoint),
                pollInterval: TimeSpan.FromHours(1),
                parentProcessIsSameProcess: static _ => true,
                observedUtcNow: ObservedUtc,
                monotonicClock: new ManualMonotonicClock(),
                tryDeleteEnvelopeIfOwned: static (_, _) => true,
                terminateProcess: () => Interlocked.Increment(ref exitCount));
            try
            {
                services.AddUnityIpcOneshotHostServices(
                    UnityIpcEndpointBinding.Create(endpoint),
                    watchdog);

                using var serviceProvider = services.BuildServiceProvider();

                Assert.That(
                    serviceProvider.GetRequiredService<OneshotProcessLifetimeWatchdog>(),
                    Is.SameAs(watchdog));
                Assert.That(
                    serviceProvider.GetRequiredService<IUnityIpcConnectionHandler>(),
                    Is.TypeOf<UnityOneshotConnectionHandler>());
                Assert.That(
                    serviceProvider.GetRequiredService<IUnityIpcServer>(),
                    Is.TypeOf<UnityIpcServer>());
                Assert.That(
                    serviceProvider.GetService<IRecoverableIpcOperationStore>(),
                    Is.Null);
                Assert.That(Volatile.Read(ref exitCount), Is.EqualTo(0));
            }
            finally
            {
                watchdog.Dispose();
            }
        }

        private static ServiceCollection CreateApplicationServices (
            out string projectPath,
            out ProjectFingerprint projectFingerprint)
        {
            var guardedProjectPath = UnityProjectPathResolver.ResolveProjectRootPath();
            projectPath = guardedProjectPath.Value;
            var storageRoot = UcliStoragePathResolver.ResolveStorageRoot(guardedProjectPath);
            projectFingerprint = UnityProjectFingerprintCalculator.Create(storageRoot, guardedProjectPath);
            var services = new ServiceCollection();
            services.AddUnityIpcApplicationServices(
                new PermitAllSessionTokenValidator(),
                projectFingerprint,
                NoOpDaemonLogger.Instance,
                DaemonEditorMode.Batchmode);
            return services;
        }

        private static IpcEndpoint CreateEndpoint (string hostKind)
        {
            return new IpcEndpoint(
                IpcTransportKind.NamedPipe,
                $"ucli-{hostKind}-composition-{Guid.NewGuid():N}");
        }
    }
}
