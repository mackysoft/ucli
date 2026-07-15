using System;
using System.IO;
using System.Threading;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Daemon;
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
            var bootstrapArguments = new IpcDaemonBootstrapArguments(
                RepositoryRoot: projectPath,
                ProjectFingerprint: projectFingerprint,
                SessionPath: Path.Combine(projectPath, "Library", "ucli-composition-root-session.json"),
                SessionGenerationId: Guid.NewGuid(),
                SessionIssuedAtUtc: ObservedUtc,
                Endpoint: endpoint);
            services.AddUnityIpcDaemonHostServices(
                bootstrapArguments,
                endpoint,
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
                storageRoot: "ucli-composition-root-tests",
                bootstrapEnvelope: new IpcOneshotBootstrapEnvelope(
                    BootstrapId: Guid.NewGuid(),
                    ParentProcessId: 42,
                    ParentProcessStartedAtUtc: ObservedUtc.AddMinutes(-1),
                    ProjectFingerprint: projectFingerprint,
                    SessionToken: IpcSessionToken.CreateRandom(),
                    CreatedAtUtc: ObservedUtc,
                    ExitDeadlineUtc: ObservedUtc.AddMinutes(1),
                    Endpoint: endpoint),
                pollInterval: TimeSpan.FromHours(1),
                parentProcessIsSameProcess: static (_, _) => true,
                observedUtcNow: ObservedUtc,
                monotonicClock: new ManualMonotonicClock(),
                tryDeleteEnvelopeIfOwned: static (_, _) => true,
                processExit: _ => Interlocked.Increment(ref exitCount));
            try
            {
                services.AddUnityIpcOneshotHostServices(endpoint, watchdog);

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
            projectPath = Path.GetFullPath(UnityProjectPathResolver.ResolveProjectRootPath());
            var storageRoot = UcliStoragePathResolver.ResolveStorageRoot(projectPath);
            projectFingerprint = UnityProjectFingerprintCalculator.Create(storageRoot, projectPath);
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
