using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Operations;

public sealed class BoundedWindowOptionsTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryCreate_WhenCursorIsInvalid_ReturnsCursorFailure ()
    {
        var invalidCursors = new[]
        {
            "not-a-cursor",
            " " + BoundedWindowCursorCodec.Encode(1),
            BoundedWindowCursorCodec.Encode(1) + " ",
            new string('A', 15),
        };

        for (var i = 0; i < invalidCursors.Length; i++)
        {
            var isValid = BoundedWindowOptions.TryCreate(
                all: false,
                limit: null,
                cursor: invalidCursors[i],
                out _,
                out var failure);

            Assert.False(isValid);
            Assert.Equal(BoundedWindowOptionsCreationFailure.InvalidCursor, failure);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryCreate_WhenLimitAndCursorAreInvalid_ReturnsLimitFailure ()
    {
        var isValid = BoundedWindowOptions.TryCreate(
            all: false,
            limit: 0,
            cursor: "not-a-cursor",
            out _,
            out var failure);

        Assert.False(isValid);
        Assert.Equal(BoundedWindowOptionsCreationFailure.LimitOutOfRange, failure);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryCreate_WhenAllConflictsWithWindowOptions_ReturnsAllFailure ()
    {
        var isValid = BoundedWindowOptions.TryCreate(
            all: true,
            limit: 1,
            cursor: null,
            out _,
            out var failure);

        Assert.False(isValid);
        Assert.Equal(BoundedWindowOptionsCreationFailure.AllConflict, failure);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CreateBounded_WhenInputsAreValid_ReturnsBoundedWindowOptions ()
    {
        var cursor = BoundedWindowCursorCodec.Encode(3);

        var options = BoundedWindowOptions.CreateBounded(
            limit: 7,
            cursor);

        Assert.Equal(7, options.Limit);
        Assert.Equal(cursor, options.Cursor);
        Assert.Equal(3, options.Offset);
    }
}
