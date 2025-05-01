namespace Ratatosk.Core.Primitives;

public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string? Error { get; }

    protected Result(bool isSuccess, string? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success()
        => new(true, null);

    public static Result Failure(string error)
        => new(false, error);

    public void EnsureSuccess()
    {
        if (IsFailure)
            throw new InvalidOperationException($"Failure: {Error}");
    }
}

public class Result<T>
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string? Error { get; }
    public T? Value { get; }

    protected Result(bool isSuccess, T? value, string? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public static Result<T> Success(T value)
        => new(true, value, null);

    public static Result<T> Failure(string error)
        => new(false, default, error);

    public void EnsureSuccess()
    {
        if (IsFailure)
            throw new InvalidOperationException($"Failure: {Error}");
    }
}
