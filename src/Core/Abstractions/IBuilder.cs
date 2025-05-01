namespace Ratatosk.Core.Abstractions;

public interface IBuilder<T>
{
    IBuilder<T> With<TValue>(string propertyName, TValue value);
    T Build();
}
