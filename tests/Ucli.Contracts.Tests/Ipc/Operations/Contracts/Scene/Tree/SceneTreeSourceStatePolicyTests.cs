using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Operations.Contracts.Scene.Tree;

public sealed class SceneTreeSourceStatePolicyTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData(SceneTreeSourceStateKind.TemporaryScene, true)]
    [InlineData(SceneTreeSourceStateKind.LoadedScene, true)]
    [InlineData(SceneTreeSourceStateKind.PersistedPreview, false)]
    [InlineData(SceneTreeSourceStateKind.ReadIndex, false)]
    public void IsLiveSourceKind_ReturnsExpectedValue (SceneTreeSourceStateKind kind, bool expected)
    {
        Assert.Equal(expected, SceneTreeSourceStatePolicy.IsLiveSourceKind(kind));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsDirtyLiveSource_ReturnsTrue_ForDirtyLoadedScene ()
    {
        var sourceState = new SceneTreeSourceState(SceneTreeSourceStateKind.LoadedScene, isDirty: true);

        Assert.True(SceneTreeSourceStatePolicy.IsDirtyLiveSource(sourceState));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsDirtyLiveSource_ReturnsFalse_ForDirtyReadIndex ()
    {
        var sourceState = new SceneTreeSourceState(SceneTreeSourceStateKind.ReadIndex, isDirty: true);

        Assert.False(SceneTreeSourceStatePolicy.IsDirtyLiveSource(sourceState));
    }
}
