using MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Application.Tests;

internal static class QueryServiceTestSupport
{
    internal static readonly Guid RequestId = Guid.Parse("9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62");

    internal static readonly Sha256Digest OperationDescriptorDigest =
        Sha256Digest.Compute("query-service-operation-descriptor"u8);

    internal static readonly DateTimeOffset ReadIndexGeneratedAtUtc =
        DateTimeOffset.Parse("2026-04-25T00:00:00+00:00");

    internal static readonly ProjectContext QueryProjectContext = ProjectContextTestFactory.CreateRepositoryFixtureProject(
        UcliConfig.CreateDefault() with
        {
            IpcDefaultTimeoutMilliseconds = 1234,
        });

    internal static RecordingOperationCatalog CreateOperationCatalog (
        string operationName,
        Sha256Digest descriptorDigest)
    {
        return new RecordingOperationCatalog
        {
            Operations =
            [
                CreateOperationDescriptor(operationName, descriptorDigest),
            ],
        };
    }

    internal static RecordingReadIndexValidationCatalogResolver CreateReadIndexCatalogResolver (
        string operationName,
        Sha256Digest descriptorDigest)
    {
        return new RecordingReadIndexValidationCatalogResolver
        {
            OperationDescriptor = CreateOperationDescriptor(operationName, descriptorDigest),
        };
    }

    internal static QueryCommandInput CreateInput (
        QueryOperationRequest operation,
        ReadIndexMode? readIndexMode = null,
        bool failFast = false)
    {
        return new QueryCommandInput(
            ProjectPath: "/repo/UnityProject",
            Mode: UnityExecutionMode.Oneshot,
            TimeoutMilliseconds: 1234,
            ReadIndexMode: readIndexMode,
            FailFast: failFast,
            Operation: operation);
    }

    private static UcliOperationDescriptor CreateOperationDescriptor (
        string operationName,
        Sha256Digest descriptorDigest)
    {
        return new UcliOperationDescriptor(
            Name: operationName,
            Kind: UcliOperationKind.Query,
            Policy: OperationPolicy.Safe,
            ArgsSchemaJson: """{"type":"object"}""",
            DescriptorDigest: descriptorDigest,
            VerdictContract: null,
            ResultSchemaJson: """{"type":"object"}""",
            Exposure: UcliOperationExposure.Public);
    }
}
