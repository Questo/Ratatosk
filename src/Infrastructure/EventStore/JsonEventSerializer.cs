using System.Text.Json;
using System.Text.Json.Nodes;
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Core.Primitives;

namespace Ratatosk.Infrastructure.EventStore;

public class JsonEventSerializer : IEventSerializer
{
    private readonly JsonSerializerOptions _serializerOptions = new() { WriteIndented = false };

    public string Serialize(DomainEvent domainEvent)
    {
        Guard.AgainstNull(domainEvent, nameof(domainEvent));

        // Add the "Type" information to the serialized data
        var json = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), _serializerOptions);
        var jsonObject = JsonSerializer.Deserialize<JsonObject>(json) ?? throw new ArgumentNullException(nameof(domainEvent));
        jsonObject["Type"] = domainEvent.GetType().AssemblyQualifiedName; // Save the fully qualified name

        return JsonSerializer.Serialize(jsonObject, _serializerOptions);
    }

    public DomainEvent Deserialize(string json)
    {
        var baseEvent = JsonSerializer.Deserialize<JsonElement>(json);

        if (!baseEvent.TryGetProperty("Type", out var typeProp))
        {
            throw new InvalidOperationException("Missing 'Type' property in event data.");
        }

        var typeName = typeProp.GetString();
        if (string.IsNullOrEmpty(typeName))
        {
            throw new InvalidOperationException("Empty 'Type' property in event data.");
        }

        var type = Type.GetType(typeName) ?? throw new InvalidOperationException($"Could not resolve type '{typeName}'.");

        return (DomainEvent)JsonSerializer.Deserialize(json, type)!;
    }
}