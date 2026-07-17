using System.Net.Sockets;
using RecipeLibrary.Infrastructure.Persistence;
using Xunit;

namespace RecipeLibrary.Web.IntegrationTests;

public sealed class SqlTransientExceptionDetectorTests
{
    [Theory]
    [InlineData(40613)]
    [InlineData(40197)]
    [InlineData(40501)]
    [InlineData(-2)]
    [InlineData(10054)]
    public void IsTransientSqlErrorNumber_KnownTransient_ReturnsTrue(int number)
    {
        Assert.True(SqlTransientExceptionDetector.IsTransientSqlErrorNumber(number));
    }

    [Theory]
    [InlineData(18456)] // login failed
    [InlineData(208)]   // invalid object name
    [InlineData(547)]   // constraint violation
    public void IsTransientSqlErrorNumber_Permanent_ReturnsFalse(int number)
    {
        Assert.False(SqlTransientExceptionDetector.IsTransientSqlErrorNumber(number));
    }

    [Fact]
    public void IsTransient_TimeoutException_ReturnsTrue()
    {
        Assert.True(SqlTransientExceptionDetector.IsTransient(new TimeoutException("timed out")));
    }

    [Fact]
    public void IsTransient_SocketException_ReturnsTrue()
    {
        Assert.True(SqlTransientExceptionDetector.IsTransient(new SocketException()));
    }

    [Fact]
    public void IsTransient_WrappedTimeout_ReturnsTrue()
    {
        var wrapped = new InvalidOperationException("outer", new TimeoutException("inner"));
        Assert.True(SqlTransientExceptionDetector.IsTransient(wrapped));
    }

    [Fact]
    public void IsTransient_ArgumentException_ReturnsFalse()
    {
        Assert.False(SqlTransientExceptionDetector.IsTransient(new ArgumentException("bad arg")));
    }

    [Fact]
    public void IsTransient_AggregateWithTimeout_ReturnsTrue()
    {
        var aggregate = new AggregateException(new InvalidOperationException("a"), new TimeoutException("b"));
        Assert.True(SqlTransientExceptionDetector.IsTransient(aggregate));
    }
}
