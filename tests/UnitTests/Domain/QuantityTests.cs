using Ratatosk.Core.Primitives;
using Ratatosk.Domain;

namespace Ratatosk.UnitTests.Domain;

[TestClass]
public class QuantityTests
{
    [TestMethod]
    public void Create_WithValidAmountAndUnit_ShouldSucceed()
    {
        var result = Quantity.Create(10, "kg");

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(10, result.Value!.Amount);
        Assert.AreEqual("kg", result.Value.Unit);
    }

    [TestMethod]
    public void Create_WithUnitInequalityByCasing_ShouldNormalizeAndSucceed()
    {
        Result<Quantity>? result;

        var validUnitsAndUnmatchedCasings = new[]
        {
            ("kg", "KG"),
            ("g", "G"),
            ("L", "l"),
            ("ml", "ML"),
            ("m", "M"),
            ("cm", "CM"),
            ("mm", "MM")
        };

        foreach (var (validUnit, unmatchedCasing) in validUnitsAndUnmatchedCasings)
        {
            result = Quantity.Create(10, unmatchedCasing!);
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(validUnit, result.Value!.Unit);
        }
    }

    [TestMethod]
    public void Create_WithNegativeAmount_ShouldFail()
    {
        var result = Quantity.Create(-5, "kg");

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual("Quantity cannot be negative", result.Error);
    }

    [TestMethod]
    public void Create_WithInvalidUnit_ShouldFail()
    {
        var result = Quantity.Create(5, "invalid_unit");

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual("Unit 'invalid_unit' is not supported", result.Error);
    }


}