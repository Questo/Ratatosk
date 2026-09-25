using Ratatosk.Domain;
using Ratatosk.Domain.Catalog;
using Ratatosk.Domain.Inventoring.Events;
using Ratatosk.Infrastructure.Serialization.Serializers;

namespace Ratatosk.UnitTests;

[TestClass]
public class JsonEventSerializerQuantityTests
{
    [TestMethod]
    public void SerializeThenDeserialize_StockAdded_ShouldRoundTripQuantity()
    {
        var serializer = new JsonEventSerializer();
        var sku = SKU.Create(SkuGenerator.Generate("TS")).Value!;
        var domainEvent = new StockAdded(Guid.NewGuid(), sku, Quantity.Pieces(10));

        var serialized = serializer.Serialize(domainEvent);
        var deserialized = (StockAdded)serializer.Deserialize(serialized);

        Assert.AreEqual(10, deserialized.Quantity.Amount);
        Assert.AreEqual("pcs", deserialized.Quantity.Unit);
    }
}
