using Ratatosk.Core.Abstractions;

namespace Ratatosk.Core.Testing;

public abstract class TestBuilder<T> : IBuilder<T>
{
    protected readonly Dictionary<string, object?> _overrides = [];

    public IBuilder<T> With<TValue>(string propertyName, TValue value)
    {
        _overrides[propertyName] = value;
        return this;
    }

    public T Build()
    {
        return Create();
    }

    protected abstract T Create();
}
