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

        /// <summary> Discovers operations through dependency injection and builds one shared snapshot. </summary>
        /// <param name="serviceProvider"> The service provider used to activate operation instances. </param>
        /// <returns> The discovered operation snapshot. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="serviceProvider" /> is <see langword="null" />. </exception>
        public static UcliOperationCatalogSnapshot Build (IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            var registrations = UcliOperationDiscoverer.Discover(serviceProvider);
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
            return new UcliOperationCatalogSnapshot(
                Registrations: registrations,
                Catalog: CreateCatalog(registrations, generatedAtUtc, includeEditLoweringOnly: false),
                RequestValidationCatalog: CreateCatalog(registrations, generatedAtUtc, includeEditLoweringOnly: true));
        }

        private static IpcOpsReadResponse CreateCatalog (
            IReadOnlyList<UcliOperationRegistration> registrations,
            DateTimeOffset generatedAtUtc,
            bool includeEditLoweringOnly)
        {
            var operations = IndexJsonOrderingPolicy.OrderOpsEntries(registrations
                .Where(registration => ShouldIncludeInCatalog(registration.Metadata.Exposure, includeEditLoweringOnly))
                .Select(static registration =>
                {
                    var describeContract = registration.Metadata.DescribeContract;
                    return new IndexOpEntryJsonContract(
                        Name: registration.Metadata.OperationName,
                        Kind: UcliOperationKindCodec.ToValue(registration.Metadata.Kind),
                        Policy: OperationPolicyCodec.ToValue(registration.Metadata.Policy),
                        ArgsSchemaJson: registration.Metadata.ArgsSchemaJson,
                        ResultSchemaJson: registration.Metadata.ResultSchemaJson,
                        Exposure: registration.Metadata.Exposure == UcliOperationExposure.Public
                            ? null
                            : UcliOperationExposureCodec.ToValue(registration.Metadata.Exposure))
                    {
                        Description = describeContract.Description,
                        Inputs = describeContract.Inputs,
                        ResultContract = describeContract.ResultContract,
                        Assurance = describeContract.Assurance,
                        CodeContract = describeContract.CodeContract,
                    };
                }));

            return new IpcOpsReadResponse(
                GeneratedAtUtc: generatedAtUtc,
                Operations: operations);
        }

        private static bool ShouldIncludeInCatalog (
            UcliOperationExposure exposure,
            bool includeEditLoweringOnly)
        {
            return exposure == UcliOperationExposure.Public
                   || (includeEditLoweringOnly && exposure == UcliOperationExposure.EditLoweringOnly);
        }
    }
}
