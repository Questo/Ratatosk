namespace Ratatosk.Application.Ordering.Models;

public class OrderLineReadModel
{
    public string Sku { get; set; } = default!;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string Currency { get; set; } = default!;

    public OrderLineReadModel() { }

    public OrderLineReadModel(string sku, int quantity, decimal unitPrice, string currency)
    {
        Sku = sku;
        Quantity = quantity;
        UnitPrice = unitPrice;
        Currency = currency;
    }
}

public class OrderReadModel
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public string Status { get; set; } = default!;
    public List<OrderLineReadModel> Lines { get; set; } = [];
    public DateTime CreatedUtc { get; set; }
    public DateTime LastUpdatedUtc { get; set; }
}
