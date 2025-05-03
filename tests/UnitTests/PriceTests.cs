using Ratatosk.Domain.Catalog.ValueObjects;

namespace Ratatosk.UnitTests;

[TestClass]
public class PriceTests
{
    [TestMethod]
    public void Create_WithValidAmountAndCurrency_ShouldSucceed()
    {
        var result = Price.Create(99.99m, "SEK");

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(99.99m, result.Value!.Amount);
        Assert.AreEqual("SEK", result.Value.Currency);
    }

    [TestMethod]
    public void Create_WithLowercaseCurrency_ShouldNormalizeAndSucceed()
    {
        var result = Price.Create(50.5m, "usd");

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("USD", result.Value!.Currency);
    }

    [TestMethod]
    public void Create_WithDefaultCurrency_ShouldSucceed()
    {
        var result = Price.Create(0m); // Should default to SEK

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("SEK", result.Value!.Currency);
    }

    [TestMethod]
    public void Create_WithNegativeAmount_ShouldFail()
    {
        var result = Price.Create(-10m, "EUR");

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual("Price cannot be negative", result.Error);
    }

    [TestMethod]
    public void Create_WithInvalidCurrencyCodeLength_ShouldFail()
    {
        var result = Price.Create(10m, "EU");

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual("Currency must be a valid 3-letter ISO code", result.Error);
    }

    [TestMethod]
    public void Create_WithUnsupportedCurrency_ShouldFail()
    {
        var result = Price.Create(10m, "XXX"); // Not in allowed list

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual("Currency 'XXX' is not supported", result.Error);
    }

    [TestMethod]
    public void Free_ShouldReturnZeroSEK()
    {
        var price = Price.Free();

        Assert.AreEqual(0m, price.Amount);
        Assert.AreEqual("SEK", price.Currency);
    }
}