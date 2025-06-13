using System.Reflection;

namespace Ratatosk.Core.BuildingBlocks;

/// <summary>
/// A base class for creating enumeration-like classes with rich domain semantics.
/// Inspired by the concept of 'smart enums', this allows defining named constants with additional behavior.
/// </summary>
public abstract class Enumeration : IComparable
{
    public string Name { get; private set; }

    public int Id { get; private set; }

    protected Enumeration(int id, string name) => (Id, Name) = (id, name);

    public override string ToString() => Name;

    public static IEnumerable<T> GetAll<T>() where T : Enumeration =>
        typeof(T).GetFields(BindingFlags.Public |
                            BindingFlags.Static |
                            BindingFlags.DeclaredOnly)
                 .Select(f => f.GetValue(null))
                 .Cast<T>();

    public override bool Equals(object? obj)
    {
        if (obj is not Enumeration otherValue)
        {
            return false;
        }

        var typeMatches = GetType().Equals(obj.GetType());
        var valueMatches = Id.Equals(otherValue.Id);

        return typeMatches && valueMatches;
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode() + Name.GetHashCode() * 23;
    }

    public int CompareTo(object? obj)
    {
        if (obj == null)
            throw new NullReferenceException($"{nameof(obj)} is null");

        return Id.CompareTo(((Enumeration)obj).Id);
    }

    // Other utility methods ...
}