using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Phases;
using UnityEditor;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Bootstraps one batchmode oneshot export and terminates Unity. </summary>
    internal static class UnityOneshotBootstrap
    {
        /// <summary> Starts one oneshot bootstrap after batchmode initialization is ready. </summary>
        /// <returns> A task that completes after the export finishes and process exit is requested. </returns>
        internal static Task Start (IpcOneshotBootstrapArguments bootstrapArguments)
        {
            if (bootstrapArguments == null)
            {
                throw new ArgumentNullException(nameof(bootstrapArguments));
            }

            return RunSafely(bootstrapArguments);
        }

        private static async Task RunSafely (IpcOneshotBootstrapArguments bootstrapArguments)
        {
            try
            {
                await Run(bootstrapArguments);
            }
            catch (Exception)
            {
                EditorApplication.Exit(1);
            }
        }

        private static async Task Run (IpcOneshotBootstrapArguments bootstrapArguments)
        {
            if (bootstrapArguments == null)
            {
                throw new ArgumentNullException(nameof(bootstrapArguments));
            }

            var snapshot = UcliOperationCatalogSnapshotBuilder.Build();
            var directoryPath = Path.GetDirectoryName(bootstrapArguments.OutputPath);
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            var json = JsonSerializer.Serialize(snapshot.Catalog, IpcJsonSerializerOptions.Default);
            await File.WriteAllTextAsync(
                bootstrapArguments.OutputPath,
                json + Environment.NewLine);
            EditorApplication.Exit(0);
        }
    }
}
