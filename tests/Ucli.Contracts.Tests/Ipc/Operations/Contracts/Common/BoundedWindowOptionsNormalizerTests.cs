using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Operations;

public sealed class BoundedWindowOptionsNormalizerTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryNormalize_WhenCursorIsInvalid_ReturnsCursorFailure ()
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
            var isValid = BoundedWindowOptionsNormalizer.TryNormalize(
                all: false,
                limit: null,
                cursor: invalidCursors[i],
                allConflictMessage: "conflict",
                cursorErrorMessage: "cursor is invalid.",
                out _,
                out var invalidField,
                out var errorMessage);

            Assert.False(isValid);
            Assert.Equal(BoundedWindowInvalidField.Cursor, invalidField);
            Assert.Equal("cursor is invalid.", errorMessage);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryNormalize_WhenLimitAndCursorAreInvalid_ReturnsLimitFailure ()
    {
        var isValid = BoundedWindowOptionsNormalizer.TryNormalize(
            all: false,
            limit: 0,
            cursor: "not-a-cursor",
            allConflictMessage: "conflict",
            cursorErrorMessage: "cursor is invalid.",
            out _,
            out var invalidField,
            out var errorMessage);

        Assert.False(isValid);
        Assert.Equal(BoundedWindowInvalidField.Limit, invalidField);
        Assert.Contains("limit must be between", errorMessage, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryNormalize_WhenAllConflictsWithWindowOptions_ReturnsAllFailure ()
    {
        var isValid = BoundedWindowOptionsNormalizer.TryNormalize(
            all: true,
            limit: 1,
            cursor: null,
            allConflictMessage: "conflict",
            cursorErrorMessage: "cursor is invalid.",
            out _,
            out var invalidField,
            out var errorMessage);

        Assert.False(isValid);
        Assert.Equal(BoundedWindowInvalidField.All, invalidField);
        Assert.Equal("conflict", errorMessage);
    }
}
