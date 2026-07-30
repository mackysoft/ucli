using System;
using System.Collections.Generic;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Cryptography;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Captures the operation contract facts fixed for one execution trace. </summary>
    internal sealed record OperationContractFacts
    {
        private OperationContractFacts (
            UcliOperationKind operationKind,
            AssuranceFacts assurance,
            Sha256Digest descriptorDigest,
            bool hasVerdictContract)
        {
            OperationKind = operationKind;
            Assurance = assurance ?? throw new ArgumentNullException(nameof(assurance));
            DescriptorDigest = descriptorDigest
                ?? throw new ArgumentNullException(nameof(descriptorDigest));
            HasVerdictContract = hasVerdictContract;
        }

        public static OperationContractFacts FromMetadata (UcliOperationMetadata metadata)
        {
            if (metadata == null)
            {
                throw new ArgumentNullException(nameof(metadata));
            }

            var assurance = metadata.DescribeContract.Assurance
                ?? throw new InvalidOperationException(
                    "Registered operation metadata must contain its validated assurance contract.");
            return new OperationContractFacts(
                metadata.Kind,
                new AssuranceFacts(
                    assurance.MayDirty,
                    assurance.MayPersist,
                    assurance.TouchedKinds),
                metadata.DescriptorDigest,
                metadata.DescribeContract.VerdictContract != null);
        }

        public UcliOperationKind OperationKind { get; }

        public AssuranceFacts Assurance { get; }

        public Sha256Digest DescriptorDigest { get; }

        public bool HasVerdictContract { get; }

        /// <summary> Captures the declared side-effect assurance used during runtime validation. </summary>
        public sealed record AssuranceFacts (
            bool MayDirty,
            bool MayPersist,
            IReadOnlyList<UcliTouchedResourceKind> TouchedKinds);
    }
}
