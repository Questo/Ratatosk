using System.Text.Json;
using System.Text.Json.Nodes;
using Ratatosk.Core.Primitives;

namespace Ratatosk.Infrastructure.Shared;

public abstract class JsonPolymorphicSerializer<TBase>
{
    private readonly JsonSerializerOptions _options = new() { WriteIndented = false };

    public string Serialize(TBase obj)
    {
        Guard.AgainstNull(obj, nameof(obj));

        var json = JsonSerializer.Serialize(obj, obj!.GetType(), _options);
        var jsonObject = JsonSerializer.Deserialize<JsonObject>(json) ?? throw new ArgumentNullException(nameof(obj));
        jsonObject["Type"] = obj.GetType().AssemblyQualifiedName;

        return JsonSerializer.Serialize(jsonObject, _options);
    }

    public TBase Deserialize(string json)
    {
        var baseObj = JsonSerializer.Deserialize<JsonElement>(json);

        if (!baseObj.TryGetProperty("Type", out var typeProp))
            throw new InvalidOperationException("Missing 'Type' property in serialized data.");

        var typeName = typeProp.GetString();
        if (string.IsNullOrEmpty(typeName))
            throw new InvalidOperationException("Empty 'Type' property in serialized data.");

        var type = Type.GetType(typeName)
                   ?? throw new InvalidOperationException($"Could not resolve type '{typeName}'.");

        return (TBase)JsonSerializer.Deserialize(json, type)!;
    }
}