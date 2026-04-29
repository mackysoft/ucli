using MackySoft.Ucli.Infrastructure.Paths;

namespace MackySoft.Ucli.Infrastructure.Tests.Paths;

public sealed class PathFormatExceptionClassifierTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData(typeof(ArgumentException))]
    [InlineData(typeof(NotSupportedException))]
    [InlineData(typeof(PathTooLongException))]
    public void IsPathFormatException_ReturnsTrue_ForPathFormatExceptions (Type exceptionType)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType, "message")!;

        var result = PathFormatExceptionClassifier.IsPathFormatException(exception);

        Assert.True(result);
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
