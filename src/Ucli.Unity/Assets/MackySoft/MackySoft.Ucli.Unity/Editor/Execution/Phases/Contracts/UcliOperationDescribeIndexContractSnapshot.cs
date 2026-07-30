using System.Collections.Generic;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary>
    /// Holds the registration-time snapshot shared by operation describe and index delivery.
    /// </summary>
    internal sealed class UcliOperationDescribeIndexContractSnapshot
    {
        private readonly UcliOperationDescribeContract describeContract;

        private readonly Sha256Digest descriptorDigest;

        private UcliOperationDescribeIndexContractSnapshot (
            UcliOperationDescribeContract describeContract,
            IndexOpEntryJsonContract indexContract)
        {
            if (describeContract == null)
            {
                throw new System.ArgumentNullException(nameof(describeContract));
            }

            if (indexContract == null)
            {
                throw new System.ArgumentNullException(nameof(indexContract));
            }

            if (indexContract.DescriptorDigest == null)
            {
                throw new System.ArgumentException(
                    "The captured index contract must contain its descriptor digest.",
                    nameof(indexContract));
            }

            this.describeContract = describeContract;
            descriptorDigest = indexContract.DescriptorDigest;
            IndexContract = indexContract;
        }

        /// <summary> Gets a defensive copy of the registered operation describe contract. </summary>
        public UcliOperationDescribeContract DescribeContract => CopyDescribeContract(describeContract);

        /// <summary> Gets the stable digest of the complete index descriptor. </summary>
        public Sha256Digest DescriptorDigest => descriptorDigest;

        /// <summary> Gets the index descriptor created from the same describe contract snapshot. </summary>
        public IndexOpEntryJsonContract IndexContract { get; }

        /// <summary> Captures one shared describe and index contract snapshot. </summary>
        public static UcliOperationDescribeIndexContractSnapshot Create (
            string operationName,
            UcliOperationKind kind,
            OperationPolicy policy,
            UcliOperationDescribeContract describeContract,
            UcliOperationExposure exposure,
            UcliOperationPlayModeSupport playModeSupport)
        {
            var describeSnapshot = CopyDescribeContract(describeContract);
            return new UcliOperationDescribeIndexContractSnapshot(
                describeSnapshot,
                CreateIndexContract(
                    operationName,
                    kind,
                    policy,
                    describeSnapshot,
                    exposure,
                    playModeSupport));
        }

        private static IndexOpEntryJsonContract CreateIndexContract (
            string operationName,
            UcliOperationKind kind,
            OperationPolicy policy,
            UcliOperationDescribeContract describeContract,
            UcliOperationExposure exposure,
            UcliOperationPlayModeSupport playModeSupport)
        {
            var descriptor = new IndexOpEntryJsonContract(
                Name: operationName,
                Kind: kind,
                Policy: policy,
                DescriptorDigest: null,
                ArgsContract: describeContract.ArgsContract,
                ResultContract: describeContract.ResultContract,
                VerdictContract: describeContract.VerdictContract,
                Exposure: exposure == UcliOperationExposure.Public
                    ? null
                    : exposure,
                PlayModeSupport: playModeSupport)
            {
                Description = describeContract.Description,
                Assurance = describeContract.Assurance,
                CodeContract = describeContract.CodeContract,
            };
            return descriptor with
            {
                DescriptorDigest = UcliOperationDescriptorDigest.Calculate(descriptor),
            };
        }

        private static UcliOperationDescribeContract CopyDescribeContract (
            UcliOperationDescribeContract source)
        {
            return new UcliOperationDescribeContract(
                source.Description,
                CopyGeneratedContract(source.ArgsContract),
                CopyGeneratedContract(source.ResultContract),
                source.VerdictContract,
                source.Assurance,
                CopyCodeContract(source.CodeContract));
        }

        private static UcliOperationJsonContract? CopyGeneratedContract (
            UcliOperationJsonContract? source)
        {
            return source;
        }

        private static UcliOperationCodeContract? CopyCodeContract (
            UcliOperationCodeContract? source)
        {
            if (source == null)
            {
                return null;
            }

            return new UcliOperationCodeContract(
                source.Language,
                CopyCodeEntryPoint(source.EntryPoint),
                CopyCodeSourceForms(source.SourceForms),
                CopyCodeApiTypes(source.ApiTypes));
        }

        private static UcliCodeEntryPointContract? CopyCodeEntryPoint (
            UcliCodeEntryPointContract? source)
        {
            if (source == null)
            {
                return null;
            }

            return new UcliCodeEntryPointContract(
                source.Signature,
                source.MatchRule,
                source.RequiredStatic,
                CopyValues(source.ParameterTypes),
                source.ReturnValue);
        }

        private static IReadOnlyList<UcliCodeSourceFormContract>? CopyCodeSourceForms (
            IReadOnlyList<UcliCodeSourceFormContract>? source)
        {
            if (source == null)
            {
                return null;
            }

            var result = new UcliCodeSourceFormContract[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                result[i] = new UcliCodeSourceFormContract(
                    source[i].Kind,
                    source[i].Description);
            }

            return result;
        }

        private static IReadOnlyList<UcliCodeApiTypeContract>? CopyCodeApiTypes (
            IReadOnlyList<UcliCodeApiTypeContract>? source)
        {
            if (source == null)
            {
                return null;
            }

            var apiTypes = new UcliCodeApiTypeContract[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                apiTypes[i] = new UcliCodeApiTypeContract(
                    source[i].Name,
                    source[i].FullName,
                    source[i].Description,
                    CopyCodeApiMembers(source[i].Members));
            }

            return apiTypes;
        }

        private static IReadOnlyList<UcliCodeApiMemberContract>? CopyCodeApiMembers (
            IReadOnlyList<UcliCodeApiMemberContract>? source)
        {
            if (source == null)
            {
                return null;
            }

            var members = new UcliCodeApiMemberContract[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                members[i] = new UcliCodeApiMemberContract(
                    source[i].Kind,
                    source[i].Name,
                    source[i].Description,
                    source[i].Type,
                    source[i].ReturnType,
                    CopyCodeApiParameters(source[i].Parameters));
            }

            return members;
        }

        private static IReadOnlyList<UcliCodeApiParameterContract>? CopyCodeApiParameters (
            IReadOnlyList<UcliCodeApiParameterContract>? source)
        {
            if (source == null)
            {
                return null;
            }

            var parameters = new UcliCodeApiParameterContract[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                parameters[i] = new UcliCodeApiParameterContract(
                    source[i].Name,
                    source[i].Type,
                    source[i].Description);
            }

            return parameters;
        }

        private static IReadOnlyList<T>? CopyValues<T> (IReadOnlyList<T>? source)
        {
            if (source == null)
            {
                return null;
            }

            var values = new T[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                values[i] = source[i];
            }

            return values;
        }
    }
}
