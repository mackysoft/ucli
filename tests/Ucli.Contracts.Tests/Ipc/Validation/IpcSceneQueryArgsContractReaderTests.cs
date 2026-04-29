using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc.Validation;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Validation;

public sealed class IpcSceneQueryArgsContractReaderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryReadForEditSelection_RejectsExplicitSceneProperty ()
    {
        using var document = JsonDocument.Parse("""{"scene":"Assets/Scenes/Main.unity","pathPrefix":"Root"}""");

        var result = IpcSceneQueryArgsContractReader.TryReadForEditSelection(
            document.RootElement,
            out _,
            out var errorMessage);

        Assert.False(result);
        Assert.Equal("Edit step property 'step.select.from.args' cannot contain property 'scene'.", errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryReadForOperation_RequiresSceneProperty ()
    {
        using var document = JsonDocument.Parse("""{"pathPrefix":"Root"}""");

        var result = IpcSceneQueryArgsContractReader.TryReadForOperation(
            document.RootElement,
            out _,
            out var errorMessage);

        Assert.False(result);
        Assert.Equal("Operation 'args.scene' is required.", errorMessage);
    }
}
