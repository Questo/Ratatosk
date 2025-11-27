using System.Text.Json;
using System.Text.Json.Serialization;
using Ratatosk.Domain;
using Ratatosk.Domain.Catalog.ValueObjects;

namespace Ratatosk.Infrastructure.Serialization.Converters;

public class ProductNameConverter : JsonConverter<ProductName>
{
    public override ProductName Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        var value = reader.GetString();
        var result = ProductName.Create(value!);

        if (!result.IsSuccess)
        {
            throw new JsonException("ProductName cannot be null");
        }

        return result.Value!;
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
        var value = reader.GetString();
        var result = SKU.Create(value!);

        if (!result.IsSuccess)
        {
            throw new JsonException("SKU cannot be null");
        }

        return result.Value!;
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
        var value = reader.GetString();
        var result = Description.Create(value!);

        if (!result.IsSuccess)
        {
            throw new JsonException("ProductDescription cannot be null");
        }

        return result.Value!;
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

        var result = Price.Create(amount.Value, currency!);

        if (!result.IsSuccess)
            throw new JsonException($"Invalid Price: {result.Error}");

        return result.Value!;
    }

    public override void Write(Utf8JsonWriter writer, Price value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("Amount", value.Amount);
        writer.WriteString("Currency", value.Currency);
        writer.WriteEndObject();
    }
}
