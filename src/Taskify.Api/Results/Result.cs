namespace Taskify.Api.Results;

public enum ErrorKind
{
    NotFound,
    Forbidden,
    Unprocessable,
}

/// <summary>An expected outcome other than success.</summary>
public sealed record Error(ErrorKind Kind, string Detail);

/// <summary>A success value, or an <see cref="Results.Error"/>.</summary>
public readonly record struct Result<T>
{
    private Result(T? value, Error? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }

    public Error? Error { get; }

    public bool IsSuccess => Error is null;

    /// <summary>
    /// For values typed as an interface, which C# does not convert implicitly.
    /// </summary>
    public static Result<T> Success(T value) => new(value, null);

    public static implicit operator Result<T>(T value) => new(value, null);

    public static implicit operator Result<T>(Error error) => new(default, error);
}

/// <summary>An outcome with no value, such as a delete.</summary>
public readonly record struct Result
{
    private Result(Error? error) => Error = error;

    public static Result Success { get; } = new(null);

    public Error? Error { get; }

    public bool IsSuccess => Error is null;

    public static implicit operator Result(Error error) => new(error);
}
