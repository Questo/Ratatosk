using Ratatosk.Core.Primitives;

namespace Ratatosk.UnitTests.Core;

[TestClass]
public class ResultTests
{
    [TestMethod]
    public void Result_Success_Should_Be_Successful()
    {
        var result = Result.Success();

        Assert.IsTrue(result.IsSuccess);
        Assert.IsFalse(result.IsFailure);
        Assert.IsNull(result.Error);
    }

    [TestMethod]
    public void Result_Failure_Should_Contain_Error()
    {
        var result = Result.Failure("Something went wrong");

        Assert.IsFalse(result.IsSuccess);
        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual("Something went wrong", result.Error);
    }

    [TestMethod]
    public void Result_EnsureSuccess_Should_Throw_On_Failure()
    {
        var result = Result.Failure("fail");
        Assert.ThrowsException<InvalidOperationException>(result.EnsureSuccess);
    }

    [TestMethod]
    public void Result_EnsureSuccess_Should_Not_Throw_On_Success()
    {
        var result = Result.Success();
        result.EnsureSuccess(); // should NOT throw
    }
}

[TestClass]
public class ResultGenericTests
{
    [TestMethod]
    public void ResultT_Success_Should_Contain_Value()
    {
        var result = Result<string>.Success("hello");

        Assert.IsTrue(result.IsSuccess);
        Assert.IsFalse(result.IsFailure);
        Assert.AreEqual("hello", result.Value);
        Assert.IsNull(result.Error);
    }

    [TestMethod]
    public void ResultT_Failure_Should_Contain_Error()
    {
        var result = Result<string>.Failure("nope");

        Assert.IsFalse(result.IsSuccess);
        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual("nope", result.Error);
        Assert.IsNull(result.Value);
    }

    [TestMethod]
    public void ResultT_EnsureSuccess_Should_Throw_On_Failure()
    {
        var result = Result<int>.Failure("bad");
        Assert.ThrowsException<InvalidOperationException>(result.EnsureSuccess);
    }

    [TestMethod]
    public void ResultT_EnsureSuccess_Should_Not_Throw_On_Success()
    {
        var result = Result<int>.Success(42);
        result.EnsureSuccess();
    }
}
