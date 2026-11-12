namespace Herensuge.Common.Domain;

public record Error(string Code, string Description, ErrorType Type)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);

    public static Error Failure(string code, string description) => new(code, description, ErrorType.Failure);

    public static Error Validation(string code, string description) => new(code, description, ErrorType.Validation);

    public static Error NotFound(string code, string description) => new(code, description, ErrorType.NotFound);

    public static Error Conflict(string code, string description) => new(code, description, ErrorType.Conflict);

    public static Error Unauthorized(string code, string description) => new(code, description, ErrorType.Unauthorized);

    public static Error Forbidden(string code, string description) => new(code, description, ErrorType.Forbidden);

    public static Error TooManyRequests(string code, string description) => new(code, description, ErrorType.TooManyRequests);
}

public enum ErrorType
{
    Failure,
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Forbidden,
    TooManyRequests,
}

public sealed record ValidationError : Error
{
    public ValidationError(IReadOnlyList<Error> errors)
        : base("General.Validation", "One or more validation errors occurred.", ErrorType.Validation)
    {
        Errors = errors;
    }

    public IReadOnlyList<Error> Errors { get; }
}
