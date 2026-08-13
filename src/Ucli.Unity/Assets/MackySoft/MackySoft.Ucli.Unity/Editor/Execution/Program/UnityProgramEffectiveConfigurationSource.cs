using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Projects;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Unity.Execution.Program
{
    /// <summary> Independently resolves the effective Program Request configuration from the Unity project's config file. </summary>
    internal interface IUnityProgramEffectiveConfigurationSource
    {
        bool TryCapture (out IpcProgramEffectiveConfigurationSnapshot? configuration);
    }

    internal sealed class UnityProgramEffectiveConfigurationSource : IUnityProgramEffectiveConfigurationSource
    {
        private static readonly IReadOnlyDictionary<string, int> DefaultTimeouts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["test"] = 300000, ["ready"] = 10000, ["compile"] = 120000, ["build.run"] = 1800000,
            ["verify"] = 120000, ["status"] = 5000, ["validate"] = 10000, ["plan"] = 20000,
            ["call"] = 60000, ["eval"] = 60000, ["program.plan"] = 20000, ["program.run"] = 1800000,
            ["program.status"] = 10000, ["program.cancel"] = 60000, ["resolve"] = 10000, ["query"] = 10000,
            ["refresh"] = 120000, ["ops"] = 120000, ["daemon.start"] = 60000, ["daemon.stop"] = 10000,
            ["daemon.cleanup"] = 3000, ["daemon.status"] = 3000, ["daemon.list"] = 3000,
            ["logs.daemon.read"] = 3000, ["logs.unity.read"] = 3000, ["logs.unity.clear"] = 3000,
            ["screenshot"] = 3000, ["screenshot.game"] = 3000, ["screenshot.scene"] = 3000,
            ["recording.start"] = 180000, ["recording.status"] = 5000, ["recording.stop"] = 60000,
            ["play.status"] = 3000, ["play.enter"] = 30000, ["play.exit"] = 30000,
        };

        private readonly UnityProjectIdentity project;

        public UnityProgramEffectiveConfigurationSource (UnityProjectIdentity project)
        {
            this.project = project ?? throw new ArgumentNullException(nameof(project));
        }

        public bool TryCapture (out IpcProgramEffectiveConfigurationSnapshot? configuration)
        {
            try
            {
                var repositoryRoot = UcliStoragePathResolver.ResolveStorageRoot(AbsolutePath.Parse(project.ProjectPath));
                var configPath = UcliStoragePathResolver.ResolveConfigPath(repositoryRoot);
                if (!File.Exists(configPath.Value))
                {
                    configuration = CreateDefault();
                    return true;
                }

                using var document = JsonDocument.Parse(File.ReadAllText(configPath.Value));
                if (!TryRead(document.RootElement, out configuration))
                {
                    configuration = null;
                    return false;
                }
                return true;
            }
            catch (Exception exception) when (exception is ArgumentException or IOException or JsonException or UnauthorizedAccessException)
            {
                configuration = null;
                return false;
            }
        }

        private static IpcProgramEffectiveConfigurationSnapshot CreateDefault () => Create(
            schemaVersion: 1,
            operationPolicy: "safe",
            planTokenMode: "optional",
            readIndexDefaultMode: "requireFresh",
            operationAllowlist: new[] { "^ucli\\." },
            ipcDefaultTimeoutMilliseconds: 3000,
            timeouts: new Dictionary<string, int>(DefaultTimeouts, StringComparer.Ordinal));

        private static bool TryRead (JsonElement root, out IpcProgramEffectiveConfigurationSnapshot? configuration)
        {
            configuration = null;
            if (!UcliConfigDocumentValidator.TryValidate(root, DefaultTimeouts, out var document))
            {
                return false;
            }

            configuration = Create(
                document!.SchemaVersion,
                document.OperationPolicy,
                document.PlanTokenMode,
                document.ReadIndexDefaultMode,
                document.OperationAllowlist,
                document.IpcDefaultTimeoutMilliseconds,
                document.IpcTimeoutMillisecondsByCommand);
            return true;
        }

        private static IpcProgramEffectiveConfigurationSnapshot Create (
            int schemaVersion,
            string operationPolicy,
            string planTokenMode,
            string readIndexDefaultMode,
            IReadOnlyList<string> operationAllowlist,
            int ipcDefaultTimeoutMilliseconds,
            IReadOnlyDictionary<string, int> timeouts)
        {
            var digest = IpcProgramEffectiveConfigurationSnapshot.ComputeDigest(
                schemaVersion, operationPolicy, planTokenMode, readIndexDefaultMode, operationAllowlist,
                ipcDefaultTimeoutMilliseconds, timeouts);
            return new IpcProgramEffectiveConfigurationSnapshot(
                schemaVersion, operationPolicy, planTokenMode, readIndexDefaultMode, operationAllowlist,
                ipcDefaultTimeoutMilliseconds, timeouts, digest);
        }
    }
}
