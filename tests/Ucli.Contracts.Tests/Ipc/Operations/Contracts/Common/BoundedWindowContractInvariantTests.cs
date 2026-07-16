using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Operations;

public sealed class BoundedWindowContractInvariantTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(BoundedWindowConstants.MaxLimit + 1)]
    public void Constructor_WhenLimitIsOutsideSupportedRange_RejectsValue (int limit)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BoundedWindow(
            limit,
            cursor: null,
            nextCursor: null,
            isComplete: true,
            totalCount: 0));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("not-a-cursor", null)]
    [InlineData(null, "not-a-cursor")]
    public void Constructor_WhenCursorIsNotCanonical_RejectsValue (
        string? cursor,
        string? nextCursor)
    {
        Assert.Throws<ArgumentException>(() => new BoundedWindow(
            limit: 1,
            cursor,
            nextCursor,
            isComplete: nextCursor is null,
            totalCount: null));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenCompletionAndNextCursorConflict_RejectsValue ()
    {
        Assert.Throws<ArgumentException>(() => new BoundedWindow(
            limit: 1,
            cursor: null,
            nextCursor: BoundedWindowCursorCodec.Encode(1),
            isComplete: true,
            totalCount: 2));
        Assert.Throws<ArgumentException>(() => new BoundedWindow(
            limit: 1,
            cursor: null,
            nextCursor: null,
            isComplete: false,
            totalCount: 2));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("cursor")]
    [InlineData("nextCursor")]
    [InlineData("isComplete")]
    public void Constructor_WhenUnboundedWindowContainsPagingState_RejectsValue (string conflict)
    {
        var cursor = conflict == "cursor" ? BoundedWindowCursorCodec.Encode(1) : null;
        var nextCursor = conflict == "nextCursor" ? BoundedWindowCursorCodec.Encode(2) : null;
        var isComplete = conflict != "isComplete";

        Assert.Throws<ArgumentException>(() => new BoundedWindow(
            limit: null,
            cursor,
            nextCursor,
            isComplete,
            totalCount: null));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenNextCursorDoesNotAdvance_RejectsValue ()
    {
        var cursor = BoundedWindowCursorCodec.Encode(2);

        Assert.Throws<ArgumentException>(() => new BoundedWindow(
            limit: 1,
            cursor,
            nextCursor: cursor,
            isComplete: false,
            totalCount: null));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenNextCursorAdvancesPastLimit_RejectsValue ()
    {
        Assert.Throws<ArgumentException>(() => new BoundedWindow(
            limit: 2,
            cursor: BoundedWindowCursorCodec.Encode(1),
            nextCursor: BoundedWindowCursorCodec.Encode(4),
            isComplete: false,
            totalCount: null));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenIncompleteNextCursorReachesKnownTotal_RejectsValue ()
    {
        Assert.Throws<ArgumentException>(() => new BoundedWindow(
            limit: 2,
            cursor: null,
            nextCursor: BoundedWindowCursorCodec.Encode(2),
            isComplete: false,
            totalCount: 2));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenTotalCountIsNegative_RejectsValue ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BoundedWindow(
            limit: null,
            cursor: null,
            nextCursor: null,
            isComplete: true,
            totalCount: -1));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void BoundedWindowOptions_ExposeOnlyValidUnboundedOrBoundedStates ()
    {
        var cursor = BoundedWindowCursorCodec.Encode(3);
        var bounded = BoundedWindowOptions.CreateBounded(limit: 7, cursor);

        Assert.Null(BoundedWindowOptions.Unbounded.Limit);
        Assert.Null(BoundedWindowOptions.Unbounded.Cursor);
        Assert.Equal(0, BoundedWindowOptions.Unbounded.Offset);
        Assert.Equal(7, bounded.Limit);
        Assert.Equal(cursor, bounded.Cursor);
        Assert.Equal(3, bounded.Offset);
        Assert.Throws<ArgumentOutOfRangeException>(() => BoundedWindowOptions.CreateBounded(0, null));
        Assert.Throws<ArgumentException>(() => BoundedWindowOptions.CreateBounded(1, "not-a-cursor"));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(nameof(BoundedWindow.Limit))]
    [InlineData(nameof(BoundedWindow.Cursor))]
    [InlineData(nameof(BoundedWindow.NextCursor))]
    [InlineData(nameof(BoundedWindow.IsComplete))]
    [InlineData(nameof(BoundedWindow.TotalCount))]
    public void Property_DoesNotExposeStateMutation (string propertyName)
    {
        Assert.Null(typeof(BoundedWindow).GetProperty(propertyName)!.SetMethod);
    }
}
