namespace Ratatosk.Core.Abstractions;

public interface ISpecification<T>
{
    bool IsSatisfiedBy(T item);
}
