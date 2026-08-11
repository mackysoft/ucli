using System.Text.Json;
using MackySoft.Ucli.Application.Features.Programs.Parsing;
using MackySoft.Ucli.Application.Features.Programs.Resolution;
using MackySoft.Ucli.Application.Features.Programs.Validate;

namespace MackySoft.Ucli.Application.Tests.Features.Programs.Validate;

public sealed class ProgramValidationServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task ValidateAsync_ProjectsOnlyTheDefinitionResolverResult ()
    {
        var expected = CreateResolutionResult();
        var resolver = new RecordingDefinitionResolver(expected);
        var service = new ProgramValidationService(resolver);
        var input = new ProgramDefinitionResolutionInput(
            "{\"steps\":[{\"command\":\"ready\"}]}",
            ProgramRootSource.Stdin,
            RootPath: null,
            PresetId: null,
            ReferenceRootPath: null);

        var actual = await service.ValidateAsync(input);

        Assert.Same(expected, actual);
        Assert.Same(input, Assert.Single(resolver.Inputs));
    }

    private static ProgramDefinitionResolutionResult CreateResolutionResult ()
    {
        using var document = JsonDocument.Parse("{\"steps\":[{\"command\":\"ready\"}]}");
        var program = new ProgramDefinition([new ReadyProgramStep(null)], document.RootElement.Clone());
        var manifest = new ProgramSourceManifest(
            Digest: "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            RootSource: ProgramRootSource.Stdin,
            RootPath: null,
            PresetId: null,
            ProgramDigest: "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
            Sources: Array.Empty<ProgramSourceManifestEntry>());
        return ProgramDefinitionResolutionResult.Success(new ResolvedProgramDefinition(
            program,
            Array.Empty<ResolvedProgramSource>(),
            manifest,
            "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc"));
    }

    private sealed class RecordingDefinitionResolver (ProgramDefinitionResolutionResult result) : IProgramDefinitionResolver
    {
        public List<ProgramDefinitionResolutionInput> Inputs { get; } = [];

        public ValueTask<ProgramDefinitionResolutionResult> ResolveAsync (
            ProgramDefinitionResolutionInput input,
            CancellationToken cancellationToken = default)
        {
            Inputs.Add(input);
            return ValueTask.FromResult(result);
        }
    }
}
