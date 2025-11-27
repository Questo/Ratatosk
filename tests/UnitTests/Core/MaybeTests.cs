using Ratatosk.Core.Primitives;

namespace Ratatosk.UnitTests.Core;

[TestClass]
public class MaybeTests
{
    [TestMethod]
    public void Some_Should_Have_Value()
    {
        var maybe = Maybe<string>.Some("hello");

        Assert.IsTrue(maybe.HasValue);
        Assert.AreEqual("hello", maybe.Value);
    }

    [TestMethod]
    public void None_Should_Have_No_Value()
    {
        var maybe = Maybe<string>.None;

        Assert.IsFalse(maybe.HasValue);
    }

    [TestMethod]
    public void Accessing_Value_On_None_Should_Throw()
    {
        var maybe = Maybe<int>.None;

        Assert.Throws<InvalidOperationException>(() => _ = maybe.Value);
    }

    [TestMethod]
    public void Match_Should_Invoke_OnSome_When_Value_Exists()
    {
        var maybe = Maybe<int>.Some(42);

        var result = maybe.Match(some => $"Got {some}", () => "Nothing");

        Assert.AreEqual("Got 42", result);
    }

    [TestMethod]
    public void Match_Should_Invoke_OnNone_When_No_Value()
    {
        var maybe = Maybe<int>.None;

        var result = maybe.Match(some => $"Got {some}", () => "Nothing");

        Assert.AreEqual("Nothing", result);
    }
}
