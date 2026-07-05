using MackySoft.Ucli.Infrastructure.Paths;

namespace MackySoft.Ucli.Infrastructure.Tests.Paths;

public sealed class PathFormatExceptionClassifierTests
{
    private static readonly Func<Exception>[] PathFormatExceptionFactories =
    [
        static () => new ArgumentException("message"),
        static () => new NotSupportedException("message"),
        static () => new PathTooLongException("message"),
    ];

    [Fact]
    [Trait("Size", "Small")]
    public void IsPathFormatException_ReturnsTrue_ForPathFormatExceptions ()
    {
        foreach (var createException in PathFormatExceptionFactories)
        {
            var result = PathFormatExceptionClassifier.IsPathFormatException(createException());

            Assert.True(result);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsPathFormatException_ReturnsFalse_ForNonPathFormatException ()
    {
        var result = PathFormatExceptionClassifier.IsPathFormatException(new IOException("io"));

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsPathFormatException_Throws_WhenExceptionIsNull ()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = PathFormatExceptionClassifier.IsPathFormatException(null!);
        });
    }
}
