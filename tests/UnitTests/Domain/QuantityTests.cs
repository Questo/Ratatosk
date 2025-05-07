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

    [TestMethod]
    public void Operator_Add_WithSameUnit_ShouldReturnCorrectResult()
    {
        var q1 = Quantity.Create(5, "pcs").Value!;
        var q2 = Quantity.Create(3, "pcs").Value!;

        var result = q1 + q2;

        Assert.AreEqual(8, result.Amount);
        Assert.AreEqual("pcs", result.Unit);
    }

    [TestMethod]
    public void Operator_Subtract_WithSameUnit_ShouldReturnCorrectResult()
    {
        var q1 = Quantity.Create(5, "pcs").Value!;
        var q2 = Quantity.Create(2, "pcs").Value!;

        var result = q1 - q2;

        Assert.AreEqual(3, result.Amount);
        Assert.AreEqual("pcs", result.Unit);
    }

    [TestMethod]
    public void Operator_Subtract_ResultingInNegative_ShouldThrow()
    {
        var q1 = Quantity.Create(2, "pcs").Value!;
        var q2 = Quantity.Create(5, "pcs").Value!;

        Assert.ThrowsException<InvalidOperationException>(() => q1 - q2);
    }

    [TestMethod]
    public void Operator_Add_WithDifferentUnits_ShouldThrow()
    {
        var q1 = Quantity.Create(5, "pcs").Value!;
        var q2 = Quantity.Create(2, "kg").Value!;

        Assert.ThrowsException<InvalidOperationException>(() => q1 + q2);
    }

    [TestMethod]
    public void Operator_Comparison_ShouldBehaveAsExpected()
    {
        var q1 = Quantity.Create(5, "kg").Value!;
        var q2 = Quantity.Create(10, "kg").Value!;
        var q3 = Quantity.Create(5, "kg").Value!;

        Assert.IsTrue(q2 > q1);
        Assert.IsTrue(q1 < q2);
        Assert.IsTrue(q1 <= q2);
        Assert.IsTrue(q2 >= q1);
        Assert.IsTrue(q1 >= q3);
        Assert.IsTrue(q1 <= q3);
    }

    [TestMethod]
    public void Operator_Comparison_WithDifferentUnits_ShouldThrow()
    {
        var q1 = Quantity.Create(5, "kg").Value!;
        var q2 = Quantity.Create(10, "pcs").Value!;

        Assert.ThrowsException<InvalidOperationException>(() => q1 > q2);
    }
}