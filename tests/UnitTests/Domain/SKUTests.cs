using Ratatosk.Domain;

namespace Ratatosk.UnitTests.Domain;

[TestClass]
public class SKUTests
{
    [TestMethod]
    public void CreateSKU_WithValidValue_ShouldSucceed()
    {
        // TS-000123-M-BLK → T-Shirt, ID 123, Medium Black
        // SH-000987-L-BLU → Shoes, ID 987, Large Blue
        // BK-X7F2K9 → Book, unique ID

        string tshirtSku = "TS-000123-M-BLK";
        string shoeSku = "SH-000987-L-BLU";
        string bookSku = "BK-X7F2K9";
        string[] validSkus = [tshirtSku, shoeSku, bookSku];

        foreach (var validSku in validSkus)
        {
            var result = SKU.Create(validSku);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(validSku, result.Value!.Value);
        }
    }

    [TestMethod]
    public void CreateSKU_WithInvalidValue_ShouldFail()
    {
        var invalidSkus = new[]
        {
            "abc-123",              // lowercase letters
            "TS--1234",             // double hyphen
            "TS-123$",              // invalid character ($)
            "TS1234",               // missing hyphen
            "T-1234",               // prefix too short
            "TOOLONG-1234",         // prefix too long
            "TS-123",               // too short second segment
            "TS-123456789",         // too long second segment
            "TS-1234-*-BLUE",       // invalid character in segment
            "TS_1234",              // underscore instead of hyphen
            "",                     // empty string
            "   ",                  // whitespace only
            null                    // null value
        };

        foreach (var invalidSku in invalidSkus)
        {
            var result = SKU.Create(invalidSku!);

            Assert.IsTrue(result.IsFailure, $"Expected failure for SKU '{invalidSku}'");
            Assert.AreEqual("Invalid SKU format", result.Error);
        }
    }

    [TestMethod]
    public void SKUs_WithSameValue_ShouldBeEqual()
    {
        var sku1 = SKU.Create("BK-X7F2K9").Value;
        var sku2 = SKU.Create("BK-X7F2K9").Value;

        Assert.AreEqual(sku1, sku2);
    }

    [TestMethod]
    public void SKUs_WithDifferentValues_ShouldNotBeEqual()
    {
        var sku1 = SKU.Create("BK-X7F2K9").Value;
        var sku2 = SKU.Create("SH-000987-L-BLU").Value;

        Assert.AreNotEqual(sku1, sku2);
    }
}
