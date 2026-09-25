using Ratatosk.Domain;
using Ratatosk.Domain.Catalog;
using Ratatosk.Domain.Catalog.ValueObjects;
using Ratatosk.Domain.Ordering;
using Ratatosk.Domain.Ordering.Events;
using Ratatosk.Infrastructure.Serialization.Serializers;

namespace Ratatosk.UnitTests;

[TestClass]
public class JsonEventSerializerOrderLineTests
{
    [TestMethod]
    public void SerializeThenDeserialize_OrderCreated_ShouldRoundTripLines()
    {
        var serializer = new JsonEventSerializer();
        var sku = SKU.Create(SkuGenerator.Generate("TS")).Value!;
        var line = OrderLine.Create(sku, 3, Price.Create(19.99m).Value!).Value!;
        var domainEvent = new OrderCreated(Guid.NewGuid(), Guid.NewGuid(), [line]);

        var serialized = serializer.Serialize(domainEvent);
        var deserialized = (OrderCreated)serializer.Deserialize(serialized);

        Assert.AreEqual(1, deserialized.Lines.Count);
        Assert.AreEqual(sku.Value, deserialized.Lines[0].Sku.Value);
        Assert.AreEqual(3, deserialized.Lines[0].Quantity);
        Assert.AreEqual(19.99m, deserialized.Lines[0].UnitPrice.Amount);
        Assert.AreEqual("SEK", deserialized.Lines[0].UnitPrice.Currency);
    }
}
