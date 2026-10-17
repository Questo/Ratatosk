using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using Ratatosk.Core.Primitives;
using Ratatosk.Infrastructure.Serialization.Converters;

namespace Ratatosk.Infrastructure.Serialization;

/// <summary>
/// Serializes a polymorphic hierarchy (events, snapshots) to JSON with a "Type" discriminator.
///
/// <para>The discriminator is the type's full name (namespace + class), which does not change
/// when the assembly version does. Records written by older versions carry the assembly-qualified
/// name; those still resolve. Moving a type to another namespace or renaming it changes its stored
/// name, so stored history must be migrated (or the old name aliased) first.</para>
///
/// <para>Value objects are rebuilt with their trusted <c>FromStored</c> factories, so tightening a
/// validation rule never makes existing history unreadable.</para>
/// </summary>
public abstract class JsonPolymorphicSerializer<TBase>
{
    private static readonly ConcurrentDictionary<string, Type> ResolvedTypes = new();

    private readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = false,
        Converters =
        {
            new ProductNameConverter(),
            new SKUConverter(),
            new DescriptionConverter(),
            new PriceConverter(),
            new QuantityConverter(),
            new OrderLineConverter(),
            new EmailConverter(),
            new PasswordHashConverter(),
        },
    };

    protected virtual IEnumerable<string> GetPreferredPropertyOrder() => [];

    public static string TypeNameOf(Type type) => type.FullName!;

    public string Serialize(TBase obj)
    {
        Guard.AgainstNull(obj, nameof(obj));

        var typeName = TypeNameOf(obj!.GetType());
        var json = JsonSerializer.Serialize(obj, obj.GetType(), _options);
        var originalJson =
            JsonSerializer.Deserialize<JsonObject>(json, _options)
            ?? throw new ArgumentNullException(nameof(obj));

        var orderedJson = new JsonObject { ["Type"] = typeName };

        foreach (var property in GetPreferredPropertyOrder())
        {
            if (originalJson.TryGetPropertyValue(property, out var value))
            {
                orderedJson[property] = value?.DeepClone();
                originalJson.Remove(property); // So it's not added again later
            }
        }

        foreach (var kvp in originalJson)
        {
            orderedJson[kvp.Key] = kvp.Value?.DeepClone();
        }

        return JsonSerializer.Serialize(orderedJson, _options);
    }

    public TBase Deserialize(string json)
    {
        var baseObj = JsonSerializer.Deserialize<JsonElement>(json, _options);

        if (!baseObj.TryGetProperty("Type", out var typeProp))
            throw new InvalidOperationException("Missing 'Type' property in serialized data.");

        var typeName = typeProp.GetString();
        if (string.IsNullOrEmpty(typeName))
            throw new InvalidOperationException("Empty 'Type' property in serialized data.");

        var type = ResolvedTypes.GetOrAdd(typeName, Resolve);

        return (TBase)JsonSerializer.Deserialize(json, type, _options)!;
    }

    private static Type Resolve(string typeName)
    {
        // Legacy records: "Namespace.Type, Assembly, Version=..., Culture=..., PublicKeyToken=...".
        var fullName = typeName.Split(',')[0].Trim();

        var type = AppDomain
            .CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(fullName))
            .FirstOrDefault(candidate => candidate is not null);

        if (type is null || !typeof(TBase).IsAssignableFrom(type))
            throw new InvalidOperationException($"Could not resolve type '{typeName}'.");

        return type;
    }
}
