namespace Ratatosk.Core.Primitives;

public readonly struct Maybe<T>
{
    private readonly T? _value;
    public bool HasValue { get; }
    public T Value => HasValue ? _value! : throw new InvalidOperationException("No value present");

    private Maybe(T value)
    {
        _value = value;
        HasValue = true;
    }

    public static Maybe<T> None => new();

    public static Maybe<T> Some(T value) => new(value);

    public TResult Match<TResult>(Func<T, TResult> onSome, Func<TResult> onNone) =>
        HasValue ? onSome(Value) : onNone();
}
