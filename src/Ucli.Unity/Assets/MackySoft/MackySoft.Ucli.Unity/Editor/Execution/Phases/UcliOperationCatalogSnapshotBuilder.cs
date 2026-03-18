using System;
using System.Collections.Generic;
using System.Linq;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Builds one shared operation snapshot from discovered registrations. </summary>
    internal static class UcliOperationCatalogSnapshotBuilder
    {
        /// <summary> Discovers operations and builds one shared snapshot. </summary>
        /// <returns> The discovered operation snapshot. </returns>
        public static UcliOperationCatalogSnapshot Build ()
        {
            var registrations = UcliOperationDiscoverer.Discover();
            return Build(registrations);
        }

        /// <summary> Builds one shared snapshot from discovered registrations. </summary>
        /// <param name="registrations"> The discovered registrations. </param>
        /// <returns> The discovered operation snapshot. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="registrations" /> is <see langword="null" />. </exception>
        public static UcliOperationCatalogSnapshot Build (IReadOnlyList<UcliOperationRegistration> registrations)
        {
            if (registrations == null)
            {
                throw new ArgumentNullException(nameof(registrations));
            }

            var generatedAtUtc = DateTimeOffset.UtcNow;
            var operations = registrations
                .OrderBy(static registration => registration.Metadata.OperationName, StringComparer.Ordinal)
                .Select(static registration => new IndexOpEntryJsonContract(
                    Name: registration.Metadata.OperationName,
                    Kind: UcliOperationKindCodec.ToValue(registration.Metadata.Kind),
                    Policy: OperationPolicyCodec.ToValue(registration.Metadata.Policy),
                    ArgsSchemaJson: registration.Metadata.ArgsSchemaJson))
                .ToArray();

            return new UcliOperationCatalogSnapshot(
                Registrations: registrations,
                Catalog: new IpcOpsReadResponse(
                    GeneratedAtUtc: generatedAtUtc,
                    Operations: operations));
        }
    }
}