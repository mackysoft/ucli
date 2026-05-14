using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Operations;

public sealed class BoundedWindowApplicatorTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Apply_WhenLimitCutsFlatList_ReturnsCursorWindowWithoutAfterField ()
    {
        var result = BoundedWindowApplicator.Apply(
            new[] { "a", "b", "c" },
            new BoundedWindowOptions(
                All: false,
                Limit: 2,
                Cursor: null,
                Offset: 0));

        Assert.Equal(new[] { "a", "b" }, result.Items);
        Assert.Equal(2, result.Window.Limit);
        Assert.Null(result.Window.Cursor);
        Assert.Equal(BoundedWindowCursorCodec.Encode(2), result.Window.NextCursor);
        Assert.False(result.Window.IsComplete);
        Assert.Equal(3, result.Window.TotalCount);
        Assert.DoesNotContain(
            typeof(BoundedWindow).GetProperties(),
            property => string.Equals(property.Name, "After", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryNormalize_WhenCursorIsInvalid_ReturnsFalse ()
    {
        var isValid = BoundedWindowOptionsNormalizer.TryNormalize(
            limit: null,
            cursor: "not-a-cursor",
            out _,
            out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("cursor is invalid.", errorMessage);
    }
}
