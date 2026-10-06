namespace OrderMgmt.Domain.Common;

public class DomainException : Exception
{
    public string Code { get; }

    public DomainException(string code, string message) : base(message)
    {
        Code = code;
    }
}

public sealed class NotFoundException : DomainException
{
    public NotFoundException(string resource, object key)
        : base("NOT_FOUND", $"{resource} with key '{key}' was not found.") { }
}

public sealed class ConflictException : DomainException
{
    public ConflictException(string message) : base("CONFLICT", message) { }
}

public sealed class ForbiddenException : DomainException
{
    public ForbiddenException(string message = "Access denied.") : base("FORBIDDEN", message) { }
}

public sealed class AuthenticationException : DomainException
{
    public AuthenticationException(string message = "Authentication failed.")
        : base("UNAUTHENTICATED", message) { }
}

public sealed class ValidationDomainException : DomainException
{
    public IDictionary<string, string[]> Errors { get; }

    public ValidationDomainException(IDictionary<string, string[]> errors)
        : base("VALIDATION", "One or more validation errors occurred.")
    {
        Errors = errors;
    }

    /// The message defaults to the first detail message, so a toast shows the actual (Vietnamese) reason.
    public ValidationDomainException(IDictionary<string, string[]> errors, string? message)
        : base("VALIDATION", message ?? errors.Values.SelectMany(v => v).FirstOrDefault() ?? "Dữ liệu không hợp lệ.")
    {
        Errors = errors;
    }
}
