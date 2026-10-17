using System.Text.Json;
using System.Text.Json.Serialization;
using Ratatosk.Domain;
using Ratatosk.Domain.Catalog.ValueObjects;
using Ratatosk.Domain.Identity;
using Ratatosk.Domain.Ordering;

namespace Ratatosk.Infrastructure.Serialization.Converters;

public class ProductNameConverter : JsonConverter<ProductName>
{
    public override ProductName Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        var value = reader.GetString() ?? throw new JsonException("ProductName cannot be null");
        return ProductName.FromStored(value);
    }

    public override void Write(
        Utf8JsonWriter writer,
        ProductName value,
        JsonSerializerOptions options
    )
    {
        writer.WriteStringValue(value.Value);
    }
}

public class SKUConverter : JsonConverter<SKU>
{
    public override SKU Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        var value = reader.GetString() ?? throw new JsonException("SKU cannot be null");
        return SKU.FromStored(value);
    }

    public override void Write(Utf8JsonWriter writer, SKU value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

public class DescriptionConverter : JsonConverter<Description>
{
    public override Description Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        var value = reader.GetString() ?? throw new JsonException("Description cannot be null");
        return Description.FromStored(value);
    }

    public override void Write(
        Utf8JsonWriter writer,
        Description value,
        JsonSerializerOptions options
    )
    {
        writer.WriteStringValue(value.Value);
    }
}

public class PriceConverter : JsonConverter<Price>
{
    public override Price Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        decimal? amount = null;
        string? currency = null;

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected start of object for Price");
        }

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected property name in Price object");
            }

            var propName = reader.GetString();
            reader.Read();

            switch (propName)
            {
                case "Amount":
                    amount = reader.GetDecimal();
                    break;
                case "Currency":
                    currency = reader.GetString();
                    break;
            }
        }

        if (amount is null || string.IsNullOrWhiteSpace(currency))
            throw new JsonException("Missing required Price properties");

        return Price.FromStored(amount.Value, currency!);
    }

    public override void Write(Utf8JsonWriter writer, Price value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("Amount", value.Amount);
        writer.WriteString("Currency", value.Currency);
        writer.WriteEndObject();
    }
}

public class QuantityConverter : JsonConverter<Quantity>
{
    public override Quantity Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        int? amount = null;
        string? unit = null;

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected start of object for Quantity");
        }

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected property name in Quantity object");
            }

            var propName = reader.GetString();
            reader.Read();

            switch (propName)
            {
                case "Amount":
                    amount = reader.GetInt32();
                    break;
                case "Unit":
                    unit = reader.GetString();
                    break;
            }
        }

        if (amount is null || string.IsNullOrWhiteSpace(unit))
            throw new JsonException("Missing required Quantity properties");

        return Quantity.FromStored(amount.Value, unit);
    }

    public override void Write(Utf8JsonWriter writer, Quantity value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("Amount", value.Amount);
        writer.WriteString("Unit", value.Unit);
        writer.WriteEndObject();
    }
}

public class OrderLineConverter : JsonConverter<OrderLine>
{
    public override OrderLine Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        SKU? sku = null;
        int? quantity = null;
        Price? unitPrice = null;

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected start of object for OrderLine");
        }

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected property name in OrderLine object");
            }

            var propName = reader.GetString();
            reader.Read();

            switch (propName)
            {
                case "Sku":
                    sku = JsonSerializer.Deserialize<SKU>(ref reader, options);
                    break;
                case "Quantity":
                    quantity = reader.GetInt32();
                    break;
                case "UnitPrice":
                    unitPrice = JsonSerializer.Deserialize<Price>(ref reader, options);
                    break;
            }
        }

        if (sku is null || quantity is null || unitPrice is null)
            throw new JsonException("Missing required OrderLine properties");

        return OrderLine.FromStored(sku, quantity.Value, unitPrice);
    }

    public override void Write(
        Utf8JsonWriter writer,
        OrderLine value,
        JsonSerializerOptions options
    )
    {
        writer.WriteStartObject();
        writer.WritePropertyName("Sku");
        JsonSerializer.Serialize(writer, value.Sku, options);
        writer.WriteNumber("Quantity", value.Quantity);
        writer.WritePropertyName("UnitPrice");
        JsonSerializer.Serialize(writer, value.UnitPrice, options);
        writer.WriteEndObject();
    }
}

// Email and PasswordHash are written as { "Value": "..." }, the shape the default serializer
// produced before these converters existed, so older records read back unchanged.
public abstract class SingleValueObjectConverter<T> : JsonConverter<T>
{
    protected abstract T FromStored(string value);
    protected abstract string ToStored(T value);

    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return FromStored(reader.GetString()!);

        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException($"Expected string or object for {typeof(T).Name}");

        string? value = null;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var propName = reader.GetString();
            reader.Read();
            if (propName == "Value")
                value = reader.GetString();
            else
                reader.Skip();
        }

        return FromStored(value ?? throw new JsonException($"Missing {typeof(T).Name} value"));
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("Value", ToStored(value));
        writer.WriteEndObject();
    }
}

public class EmailConverter : SingleValueObjectConverter<Email>
{
    protected override Email FromStored(string value) => Email.FromStored(value);

    protected override string ToStored(Email value) => value.Value;
}

public class PasswordHashConverter : SingleValueObjectConverter<PasswordHash>
{
    protected override PasswordHash FromStored(string value) => PasswordHash.FromStored(value);

    protected override string ToStored(PasswordHash value) => value.Value;
}
